using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Luizio.iFX.UnitTests.RabbitMqSubscriberTests;

[TestClass]
public class HandleMessageTests
{
    private const ulong DeliveryTag = 7UL;
    private const string EventTypeHeader = "x-event-type";
    private const string RetryCountHeader = "x-retry-count";

    private static readonly Dictionary<string, Type> TransitionTypes = new()
    {
        [typeof(TestTransitionStartedEvent).FullName!] = typeof(TestTransitionStartedEvent),
        [typeof(TestTransitionFinishedEvent).FullName!] = typeof(TestTransitionFinishedEvent)
    };

    /// <summary>An interface subscription whose invoker records what the handler was passed.</summary>
    private static Subscription BuildInterfaceSubscription(
        List<object> received,
        Error? result = null,
        int retryCount = 3,
        string? deadLetterQueue = null)
    {
        return new Subscription
        {
            Invoker = (sp, cu, msg) =>
            {
                received.Add(msg);
                return Task.FromResult(result ?? Error.Empty);
            },
            EventType = typeof(ITestTransitionEvent),
            MethodName = nameof(TestTransitionSubscriberService.OnTransition),
            Service = typeof(TestTransitionSubscriberService).Name.ToLower(),
            QueueTopic = typeof(ITestTransitionEvent).FullName!,
            BoundExchanges = [.. TransitionTypes.Keys],
            TypesByExchange = TransitionTypes,
            DeadLetterQueue = deadLetterQueue,
            RetryCount = retryCount
        };
    }

    private static BasicDeliverEventArgs BuildDelivery(
        string exchange,
        object body,
        string? eventTypeHeader = null,
        int? retryCount = null)
    {
        var headers = new Dictionary<string, object?>();
        if (eventTypeHeader is not null)
            headers[EventTypeHeader] = Encoding.UTF8.GetBytes(eventTypeHeader);
        if (retryCount is not null)
            headers[RetryCountHeader] = retryCount.Value;

        var properties = new BasicProperties { Persistent = true, Headers = headers };
        var payload = body as string ?? JsonSerializer.Serialize(body);

        return new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: DeliveryTag,
            redelivered: false,
            exchange: exchange,
            routingKey: string.Empty,
            properties: properties,
            body: Encoding.UTF8.GetBytes(payload));
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

    private static Mock<IRabbitMqChannel> BuildChannel()
    {
        var channel = new Mock<IRabbitMqChannel>();
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>())).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()))
            .Returns(ValueTask.CompletedTask);
        return channel;
    }

    [TestMethod]
    public async Task DeserializesIntoTheConcreteTypeNamedByTheExchange()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        var id = Guid.NewGuid();
        var ea = BuildDelivery(
            typeof(TestTransitionStartedEvent).FullName!,
            new TestTransitionStartedEvent { Id = id, StartedBy = "alice" });

        await HandleAsync(BuildChannel().Object, ea, subscription);

        var message = (TestTransitionStartedEvent)received.Single();
        Assert.AreEqual(id, message.Id);
        // The concrete type's own fields survive, not just the interface's.
        Assert.AreEqual("alice", message.StartedBy);
    }

    [TestMethod]
    public async Task ResolvesADifferentConcreteTypeOnTheSameSubscription()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        var ea = BuildDelivery(typeof(TestTransitionFinishedEvent).FullName!, new TestTransitionFinishedEvent { Id = Guid.NewGuid() });

        await HandleAsync(BuildChannel().Object, ea, subscription);

        Assert.IsInstanceOfType<TestTransitionFinishedEvent>(received.Single());
    }

    [TestMethod]
    public async Task PrefersTheEventTypeHeaderOverTheDeliveryExchange()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        // A retried message arrives from the default exchange and carries its type in the header.
        var ea = BuildDelivery(
            exchange: string.Empty,
            body: new TestTransitionStartedEvent { Id = Guid.NewGuid() },
            eventTypeHeader: typeof(TestTransitionStartedEvent).FullName);

        await HandleAsync(BuildChannel().Object, ea, subscription);

        Assert.IsInstanceOfType<TestTransitionStartedEvent>(received.Single());
    }

    [TestMethod]
    public async Task RejectsAnUnmappedEventType_WithoutInvokingTheHandler()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestUnrelatedEvent).FullName!, new TestUnrelatedEvent());

        await HandleAsync(channel.Object, ea, subscription);

        Assert.AreEqual(0, received.Count);
        channel.Verify(c => c.BasicNackAsync(DeliveryTag, false, false), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [TestMethod]
    public async Task DoesNotResolveATypeByReflectingOnTheHeader()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        var channel = BuildChannel();
        // A real, loadable type that is not in the allowlist. It must not be instantiated.
        var ea = BuildDelivery(
            exchange: typeof(TestTransitionStartedEvent).FullName!,
            body: new TestUnrelatedEvent(),
            eventTypeHeader: typeof(TestUnrelatedEvent).AssemblyQualifiedName);

        await HandleAsync(channel.Object, ea, subscription);

        Assert.AreEqual(0, received.Count);
        channel.Verify(c => c.BasicNackAsync(DeliveryTag, false, false), Times.Once);
    }

    [TestMethod]
    public async Task RejectsAMalformedPayload_WithoutRetrying()
    {
        var received = new List<object>();
        var subscription = BuildInterfaceSubscription(received);
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, body: "{ not json");

        await HandleAsync(channel.Object, ea, subscription);

        Assert.AreEqual(0, received.Count);
        channel.Verify(c => c.BasicNackAsync(DeliveryTag, false, false), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [TestMethod]
    public async Task AcksOnSuccess()
    {
        var subscription = BuildInterfaceSubscription([]);
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent());

        await HandleAsync(channel.Object, ea, subscription);

        channel.Verify(c => c.BasicAckAsync(DeliveryTag, false), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task NacksABusinessErrorWithoutRequeue_SoItDeadLetters()
    {
        var subscription = BuildInterfaceSubscription([], new Error(ErrorCode.Error, "rejected"),
            deadLetterQueue: $"{typeof(ITestTransitionEvent).FullName}_dlq");
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent());

        await HandleAsync(channel.Object, ea, subscription);

        channel.Verify(c => c.BasicNackAsync(DeliveryTag, false, false), Times.Once);
        // A business error reaches the same conclusion on redelivery, so it is not retried.
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [TestMethod]
    public async Task RetriesAnExceptionToTheFailingQueueOnly()
    {
        var subscription = BuildInterfaceSubscription([], new Error(ErrorCode.Exception, "boom"));
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent());

        await HandleAsync(channel.Object, ea, subscription);

        // Default exchange, routed by queue name — never back to the fanout, which would
        // re-trigger every other subscriber of the event.
        channel.Verify(c => c.BasicPublishAsync(
            string.Empty, subscription.QueueName, true, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task AcksTheOriginalWhenRetrying_SoItIsNotDeadLetteredAsWell()
    {
        var subscription = BuildInterfaceSubscription([], new Error(ErrorCode.Exception, "boom"),
            deadLetterQueue: $"{typeof(ITestTransitionEvent).FullName}_dlq");
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent());

        await HandleAsync(channel.Object, ea, subscription);

        channel.Verify(c => c.BasicAckAsync(DeliveryTag, false), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task StampsTheRetryWithItsCountAndResolvedType()
    {
        var subscription = BuildInterfaceSubscription([], new Error(ErrorCode.Exception, "boom"));
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent(), retryCount: 1);

        await HandleAsync(channel.Object, ea, subscription);

        channel.Verify(c => c.BasicPublishAsync(
            string.Empty, subscription.QueueName, true,
            It.Is<BasicProperties>(p =>
                Convert.ToInt32(p.Headers![RetryCountHeader]) == 2 &&
                Encoding.UTF8.GetString((byte[])p.Headers![EventTypeHeader]!) == typeof(TestTransitionStartedEvent).FullName &&
                p.Persistent),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task NacksWithoutRetrying_WhenTheRetryBudgetIsExhausted()
    {
        var subscription = BuildInterfaceSubscription([], new Error(ErrorCode.Exception, "boom"), retryCount: 3);
        var channel = BuildChannel();
        var ea = BuildDelivery(typeof(TestTransitionStartedEvent).FullName!, new TestTransitionStartedEvent(), retryCount: 3);

        await HandleAsync(channel.Object, ea, subscription);

        channel.Verify(c => c.BasicNackAsync(DeliveryTag, false, false), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [TestMethod]
    public async Task DoesNotLeakFrameworkHeadersIntoUserMetadata()
    {
        CurrentUser? captured = null;
        var subscription = BuildInterfaceSubscription([]);
        subscription.Invoker = (sp, cu, msg) =>
        {
            captured = cu;
            return Task.FromResult(Error.Empty);
        };
        var ea = BuildDelivery(
            typeof(TestTransitionStartedEvent).FullName!,
            new TestTransitionStartedEvent(),
            eventTypeHeader: typeof(TestTransitionStartedEvent).FullName,
            retryCount: 1);
        ea.BasicProperties.Headers!["trace-id"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new List<string> { "abc" }));

        await HandleAsync(BuildChannel().Object, ea, subscription);

        Assert.IsNotNull(captured);
        CollectionAssert.AreEquivalent(new[] { "trace-id" }, captured.Metadata.Select(m => m.Key).ToArray());
        Assert.AreEqual("abc", captured.Metadata.Single().Value);
    }
}
