using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Luizio.iFX.UnitTests.RabbitMqSubscriberTests;

[TestClass]
public class TelemetryTests
{
    private const string ConsumedCounter = "messaging_events_consumed";
    private const string RetryCountHeader = "x-retry-count";

    private static readonly Dictionary<string, Type> TransitionTypes = new()
    {
        [typeof(TestTransitionStartedEvent).FullName!] = typeof(TestTransitionStartedEvent)
    };

    private static Subscription BuildSubscription(Error? result = null, int retryCount = 3, string? deadLetterQueue = null)
        => new()
        {
            Invoker = (sp, cu, msg) => Task.FromResult(result ?? Error.Empty),
            EventType = typeof(ITestTransitionEvent),
            MethodName = nameof(TestTransitionSubscriberService.OnTransition),
            Service = typeof(TestTransitionSubscriberService).Name.ToLower(),
            QueueTopic = typeof(ITestTransitionEvent).FullName!,
            BoundExchanges = [.. TransitionTypes.Keys],
            TypesByExchange = TransitionTypes,
            DeadLetterQueue = deadLetterQueue,
            RetryCount = retryCount
        };

    private static BasicDeliverEventArgs BuildDelivery(
        string? exchange = null,
        object? retryCountHeader = null,
        string? traceParent = null,
        string? payload = null)
    {
        var headers = new Dictionary<string, object?>();
        if (retryCountHeader is not null) headers[RetryCountHeader] = retryCountHeader;
        if (traceParent is not null) headers["traceparent"] = Encoding.UTF8.GetBytes(traceParent);

        return new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 7UL,
            redelivered: false,
            exchange: exchange ?? typeof(TestTransitionStartedEvent).FullName!,
            routingKey: string.Empty,
            properties: new BasicProperties { Persistent = true, Headers = headers },
            body: Encoding.UTF8.GetBytes(payload ?? JsonSerializer.Serialize(new TestTransitionStartedEvent())));
    }

    private static Mock<IRabbitMqChannel> BuildChannel()
    {
        var channel = new Mock<IRabbitMqChannel>();
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>())).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()))
            .Returns(ValueTask.CompletedTask);
        return channel;
    }

    private static async Task HandleAsync(IRabbitMqChannel channel, BasicDeliverEventArgs ea, Subscription subscription)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUser>();
        services.AddSingleton(new SubscriptionStore());
        await using var sp = services.BuildServiceProvider();

        var subscriber = new RabbitMqSubscriber(sp, new Mock<IRabbitMqConnectionFactory>().Object, NullLogger<RabbitMqSubscriber>.Instance);
        var handle = typeof(RabbitMqSubscriber).GetMethod("HandleMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)handle.Invoke(subscriber, [channel, ea, subscription])!;
    }

    // --- Fix 1: the retry counter must never throw on a wire value ---

    [TestMethod]
    public async Task RetryCountFromAStringHeaderIsHonoured()
    {
        // A publisher that writes the header as a string delivers it as byte[].
        var subscription = BuildSubscription(new Error(ErrorCode.Exception, "boom"), retryCount: 3);
        var channel = BuildChannel();

        await HandleAsync(channel.Object, BuildDelivery(retryCountHeader: Encoding.UTF8.GetBytes("3")), subscription);

        // 3 + 1 = 4 > 3, so the budget is spent: nack, no retry.
        channel.Verify(c => c.BasicNackAsync(7UL, false, false), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [TestMethod]
    public async Task GarbageRetryCountIsTreatedAsAFirstAttempt_AndNeverStallsTheConsumer()
    {
        var subscription = BuildSubscription(new Error(ErrorCode.Exception, "boom"), retryCount: 3);
        var channel = BuildChannel();

        await HandleAsync(channel.Object, BuildDelivery(retryCountHeader: Encoding.UTF8.GetBytes("not-a-number")), subscription);

        // Previously Convert.ToInt32 threw here, leaving the delivery unacked forever.
        channel.Verify(c => c.BasicPublishAsync(string.Empty, subscription.QueueName, true, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(7UL, false), Times.Once);
    }

    [TestMethod]
    public async Task NegativeRetryCountCannotBuyAnUnlimitedBudget()
    {
        var subscription = BuildSubscription(new Error(ErrorCode.Exception, "boom"), retryCount: 3);
        var channel = BuildChannel();

        await HandleAsync(channel.Object, BuildDelivery(retryCountHeader: -100), subscription);

        channel.Verify(c => c.BasicPublishAsync(
            string.Empty, subscription.QueueName, true,
            It.Is<BasicProperties>(p => Convert.ToInt32(p.Headers![RetryCountHeader]) == 1),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    // --- Fix 4: outcome counters ---

    [TestMethod]
    public async Task CountsASuccessAsProcessed()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var subscription = BuildSubscription();

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        var measurement = metrics.For(ConsumedCounter).Single();
        Assert.AreEqual(1, measurement.Value);
        Assert.AreEqual("processed", measurement.Tag("outcome"));
        Assert.AreEqual(subscription.QueueName, measurement.Tag("queue"));
        Assert.AreEqual(typeof(TestTransitionStartedEvent).FullName, measurement.Tag("event_type"));
    }

    [TestMethod]
    public async Task CountsARetry()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var subscription = BuildSubscription(new Error(ErrorCode.Exception, "boom"));

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        Assert.AreEqual("retried", metrics.For(ConsumedCounter).Single().Tag("outcome"));
    }

    [TestMethod]
    public async Task CountsABusinessErrorAsDeadLettered_WhenADlqIsConfigured()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var subscription = BuildSubscription(new Error(ErrorCode.Error, "rejected"), deadLetterQueue: "dlq");

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        Assert.AreEqual("dead_lettered", metrics.For(ConsumedCounter).Single().Tag("outcome"));
    }

    [TestMethod]
    public async Task CountsABusinessErrorAsDiscarded_WhenNoDlqIsConfigured()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var subscription = BuildSubscription(new Error(ErrorCode.Error, "rejected"));

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        Assert.AreEqual("discarded", metrics.For(ConsumedCounter).Single().Tag("outcome"));
    }

    [TestMethod]
    public async Task DoesNotTagAnUnmappedTypeWithItsWireSuppliedName()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var subscription = BuildSubscription();

        await HandleAsync(BuildChannel().Object, BuildDelivery(exchange: "Attacker.Chosen.Name"), subscription);

        // Using the wire value here would let a publisher drive unbounded metric cardinality.
        var measurement = metrics.For(ConsumedCounter).Single();
        Assert.AreEqual("unknown", measurement.Tag("event_type"));
    }

    // --- Fix 5: tracing ---

    [TestMethod]
    public async Task CreatesAConsumerSpanForEveryDelivery()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var subscription = BuildSubscription();

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        var span = activities.Single(ActivityKind.Consumer);
        Assert.AreEqual($"process {subscription.QueueName}", span.DisplayName);
        Assert.AreEqual("rabbitmq", span.GetTagItem("messaging.system"));
        Assert.AreEqual("process", span.GetTagItem("messaging.operation.type"));
        Assert.AreEqual(subscription.QueueName, span.GetTagItem("messaging.destination.subscription.name"));
        Assert.AreEqual(typeof(TestTransitionStartedEvent).FullName, span.GetTagItem("messaging.message.type"));
    }

    [TestMethod]
    public async Task ConsumerSpanIsItsOwnTrace_LinkedToThePublish()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var producerTraceId = ActivityTraceId.CreateRandom();
        var producerSpanId = ActivitySpanId.CreateRandom();
        var traceParent = $"00-{producerTraceId}-{producerSpanId}-01";

        await HandleAsync(BuildChannel().Object, BuildDelivery(traceParent: traceParent), BuildSubscription());

        var span = activities.Single(ActivityKind.Consumer);
        // A separate trace, deliberately: consuming runs on its own schedule.
        Assert.AreNotEqual(producerTraceId, span.TraceId);
        Assert.IsNull(span.ParentId);
        // ...but navigable from the publish via a link.
        var link = span.Links.Single();
        Assert.AreEqual(producerTraceId, link.Context.TraceId);
        Assert.AreEqual(producerSpanId, link.Context.SpanId);
    }

    [TestMethod]
    public async Task ConsumerSpanHasNoLink_WhenThePublisherSentNoTraceContext()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);

        await HandleAsync(BuildChannel().Object, BuildDelivery(), BuildSubscription());

        Assert.AreEqual(0, activities.Single(ActivityKind.Consumer).Links.Count());
    }

    [TestMethod]
    public async Task MarksTheSpanFailedAndRecordsTheErrorCode()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);

        await HandleAsync(BuildChannel().Object, BuildDelivery(), BuildSubscription(new Error(ErrorCode.NotFound, "missing")));

        var span = activities.Single(ActivityKind.Consumer);
        Assert.AreEqual(ActivityStatusCode.Error, span.Status);
        Assert.AreEqual("NotFound", span.GetTagItem("error.type"));
        Assert.AreEqual("discarded", span.GetTagItem("messaging.ifx.outcome"));
    }

    [TestMethod]
    public async Task RecordsTheThrownExceptionTypeRatherThanTheErrorCode()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var subscription = BuildSubscription();
        subscription.Invoker = (sp, cu, msg) => throw new InvalidTimeZoneException("bad zone");

        await HandleAsync(BuildChannel().Object, BuildDelivery(retryCountHeader: 99), subscription);

        var span = activities.Single(ActivityKind.Consumer);
        // "Exception" is the ErrorCode name and identical for every crash — useless for grouping.
        Assert.AreEqual(typeof(InvalidTimeZoneException).FullName, span.GetTagItem("error.type"));
        Assert.AreEqual(ActivityStatusCode.Error, span.Status);
    }

    [TestMethod]
    public async Task AttachesTheExceptionToTheSpanAsAnEvent()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var subscription = BuildSubscription();
        subscription.Invoker = (sp, cu, msg) => throw new InvalidTimeZoneException("bad zone");

        await HandleAsync(BuildChannel().Object, BuildDelivery(retryCountHeader: 99), subscription);

        var exceptionEvent = activities.Single(ActivityKind.Consumer).Events
            .Single(e => e.Name == "exception");
        Assert.AreEqual(
            typeof(InvalidTimeZoneException).FullName,
            exceptionEvent.Tags.Single(t => t.Key == "exception.type").Value);
    }

    [TestMethod]
    public async Task RecordsTheExceptionOnARetryToo()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var subscription = BuildSubscription(retryCount: 3);
        subscription.Invoker = (sp, cu, msg) => throw new InvalidTimeZoneException("bad zone");

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        var span = activities.Single(ActivityKind.Consumer);
        Assert.AreEqual("retried", span.GetTagItem("messaging.ifx.outcome"));
        Assert.AreEqual(typeof(InvalidTimeZoneException).FullName, span.GetTagItem("error.type"));
    }

    [TestMethod]
    public async Task HandlerRunsInsideTheConsumerSpan_SoNestedProxySpansAreCaptured()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        ActivityTraceId capturedTraceId = default;
        var subscription = BuildSubscription();
        subscription.Invoker = (sp, cu, msg) =>
        {
            // InProcServiceProxy only starts a span when Activity.Current is non-null, which is
            // why the message path produced no spans at all before.
            capturedTraceId = Activity.Current?.TraceId ?? default;
            return Task.FromResult(Error.Empty);
        };

        await HandleAsync(BuildChannel().Object, BuildDelivery(), subscription);

        var span = activities.Single(ActivityKind.Consumer);
        Assert.AreEqual(span.TraceId, capturedTraceId);
    }
}
