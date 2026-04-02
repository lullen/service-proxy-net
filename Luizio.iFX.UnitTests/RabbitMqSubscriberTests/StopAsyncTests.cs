using Luizio.iFX.Client;
using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace Luizio.iFX.UnitTests.RabbitMqSubscriberTests;

[TestClass]
public class StopAsyncTests
{
    private static ServiceProvider BuildServiceProvider(SubscriptionStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUser>();
        services.AddSingleton(store);
        return services.BuildServiceProvider();
    }

    private static SubscriptionStore BuildStoreWithSubscription()
    {
        var store = new SubscriptionStore();
        var subscription = new Subscription
        {
            ServiceType = typeof(TestSubscriberService),
            Service = typeof(TestSubscriberService).Name.ToLower(),
            Method = typeof(TestSubscriberService).GetMethod(nameof(TestSubscriberService.Handle))!,
            Topic = typeof(TestEvent).FullName!,
            RetryCount = 3,
            PrefetchCount = 0
        };
        var field = typeof(SubscriptionStore).GetField("_subscriptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var list = (IList<Subscription>)field.GetValue(store)!;
        list.Add(subscription);
        return store;
    }

    private static (Mock<IRabbitMqConnectionFactory>, Mock<IRabbitMqConnection>, Mock<IRabbitMqChannel>) BuildMocks()
    {
        var mockFactory = new Mock<IRabbitMqConnectionFactory>();
        var mockConnection = new Mock<IRabbitMqConnection>();
        var mockChannel = new Mock<IRabbitMqChannel>();

        mockFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockConnection.Object);
        mockConnection.Setup(c => c.CreateChannelAsync())
            .ReturnsAsync(mockChannel.Object);
        mockChannel.Setup(c => c.CreateConsumer()).Returns(new Mock<IRabbitMqConsumer>().Object);
        mockChannel.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object?>>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IRabbitMqConsumer>()))
            .ReturnsAsync("consumer-tag");
        mockChannel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        mockConnection.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockConnection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return (mockFactory, mockConnection, mockChannel);
    }

    [TestMethod]
    public async Task DisposesAllChannels()
    {
        var (mockFactory, _, mockChannel) = BuildMocks();
        var store = BuildStoreWithSubscription();
        await using var sp = BuildServiceProvider(store);

        var subscriber = new RabbitMqSubscriber(sp, mockFactory.Object, NullLogger<RabbitMqSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);
        await subscriber.StopAsync(CancellationToken.None);

        mockChannel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task ClosesAndDisposesConnection()
    {
        var (mockFactory, mockConnection, _) = BuildMocks();
        var store = BuildStoreWithSubscription();
        await using var sp = BuildServiceProvider(store);

        var subscriber = new RabbitMqSubscriber(sp, mockFactory.Object, NullLogger<RabbitMqSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);
        await subscriber.StopAsync(CancellationToken.None);

        mockConnection.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockConnection.Verify(c => c.DisposeAsync(), Times.Once);
    }
}
