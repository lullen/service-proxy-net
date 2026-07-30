using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Moq;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Luizio.iFX.UnitTests.RabbitMqPublisherTests;

[TestClass]
public class PublishTests
{
    private const string EventTypeHeader = "x-event-type";

    private static (RabbitMqPublisher, Mock<IRabbitMqChannel>) BuildPublisher()
    {
        var factory = new Mock<IRabbitMqConnectionFactory>();
        var connection = new Mock<IRabbitMqConnection>();
        var channel = new Mock<IRabbitMqChannel>();

        factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        connection.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateChannelAsync()).ReturnsAsync(channel.Object);
        channel.Setup(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()))
            .Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return (new RabbitMqPublisher(factory.Object), channel);
    }

    [TestMethod]
    public async Task PublishesToTheExchangeNamedAfterTheRuntimeType()
    {
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent { Id = Guid.NewGuid() }, new CurrentUser());

        channel.Verify(c => c.BasicPublishAsync(
            typeof(TestTransitionStartedEvent).FullName!, string.Empty, false,
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task StampsTheConcreteEventTypeHeader()
    {
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());

        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.Is<BasicProperties>(p =>
                Encoding.UTF8.GetString((byte[])p.Headers![EventTypeHeader]!) == typeof(TestTransitionStartedEvent).FullName),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task StillCarriesCurrentUserMetadata()
    {
        var (publisher, channel) = BuildPublisher();
        var currentUser = new CurrentUser { Metadata = [new("trace-id", "abc")] };

        await publisher.Publish(new TestTransitionStartedEvent(), currentUser);

        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.Is<BasicProperties>(p =>
                JsonSerializer.Deserialize<List<string>>((string)p.Headers!["trace-id"]!)!.Single() == "abc"),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task CreatesAProducerSpanAndInjectsTraceContext()
    {
        using var activities = new ActivityCapture(MessagingActivitySource.SourceName);
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());

        var span = activities.Single(ActivityKind.Producer);
        Assert.AreEqual($"publish {typeof(TestTransitionStartedEvent).FullName}", span.DisplayName);
        Assert.AreEqual("publish", span.GetTagItem("messaging.operation.type"));

        // The injected traceparent must name this span, or the consumer's link points nowhere.
        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.Is<BasicProperties>(p =>
                Encoding.UTF8.GetString((byte[])p.Headers!["traceparent"]!)
                    == $"00-{span.TraceId}-{span.SpanId}-01"),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task DeclaresTheExchangeOnFirstPublish()
    {
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());

        channel.Verify(c => c.ExchangeDeclareAsync(
            typeof(TestTransitionStartedEvent).FullName!, ExchangeType.Fanout, true), Times.Once);
    }

    [TestMethod]
    public async Task DeclaresEachExchangeOnlyOnce()
    {
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());
        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());
        await publisher.Publish(new TestTransitionFinishedEvent(), new CurrentUser());

        channel.Verify(c => c.ExchangeDeclareAsync(
            typeof(TestTransitionStartedEvent).FullName!, ExchangeType.Fanout, true), Times.Once);
        channel.Verify(c => c.ExchangeDeclareAsync(
            typeof(TestTransitionFinishedEvent).FullName!, ExchangeType.Fanout, true), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()),
            Times.Exactly(3));
    }

    [TestMethod]
    public async Task CountsThePublish()
    {
        using var metrics = new MetricCapture(MessagingMeter.MeterName);
        var (publisher, _) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());

        var measurement = metrics.For("messaging_events_published").Single();
        Assert.AreEqual(1, measurement.Value);
        Assert.AreEqual(typeof(TestTransitionStartedEvent).FullName, measurement.Tag("exchange"));
    }

    [TestMethod]
    public async Task TraceContextIsNotInjected_WhenNothingIsListening()
    {
        // No ActivityCapture: StartActivity returns null, and publishing must still work.
        var (publisher, channel) = BuildPublisher();

        await publisher.Publish(new TestTransitionStartedEvent(), new CurrentUser());

        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.Is<BasicProperties>(p => !p.Headers!.ContainsKey("traceparent")),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [TestMethod]
    public async Task FrameworkHeaderWinsOverACollidingMetadataKey()
    {
        var (publisher, channel) = BuildPublisher();
        var currentUser = new CurrentUser { Metadata = [new(EventTypeHeader, "spoofed")] };

        await publisher.Publish(new TestTransitionStartedEvent(), currentUser);

        channel.Verify(c => c.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.Is<BasicProperties>(p =>
                Encoding.UTF8.GetString((byte[])p.Headers![EventTypeHeader]!) == typeof(TestTransitionStartedEvent).FullName),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }
}
