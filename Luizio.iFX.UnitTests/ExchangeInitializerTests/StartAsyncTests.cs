using Luizio.iFX.Messaging;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;

namespace Luizio.iFX.UnitTests.ExchangeInitializerTests;

[TestClass]
public class StartAsyncTests
{
    [TestMethod]
    public async Task DeclaresExchangeForEachIEventImplementation()
    {
        var mockFactory = new Mock<IRabbitMqConnectionFactory>();
        var mockConnection = new Mock<IRabbitMqConnection>();
        var mockChannel = new Mock<IRabbitMqChannel>();

        mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConnection.Object);
        mockConnection.Setup(c => c.CreateChannelAsync())
            .ReturnsAsync(mockChannel.Object);
        mockChannel.Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        mockConnection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var initializer = new ExchangeInitializer(mockFactory.Object, NullLogger<ExchangeInitializer>.Instance);
        await initializer.StartAsync(CancellationToken.None);

        mockChannel.Verify(c => c.ExchangeDeclareAsync(
            typeof(TestEvent).FullName!,
            ExchangeType.Fanout,
            true), Times.Once);
    }

    [TestMethod]
    public async Task LogsErrorAndRethrows_WhenConnectionFails()
    {
        var mockFactory = new Mock<IRabbitMqConnectionFactory>();
        mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connection refused"));

        var initializer = new ExchangeInitializer(mockFactory.Object, NullLogger<ExchangeInitializer>.Instance);

        await Assert.ThrowsAsync<Exception>(() => initializer.StartAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task StopAsync_ReturnsCompletedTask()
    {
        var mockFactory = new Mock<IRabbitMqConnectionFactory>();
        var initializer = new ExchangeInitializer(mockFactory.Object, NullLogger<ExchangeInitializer>.Instance);

        await initializer.StopAsync(CancellationToken.None);
    }
}
