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
public class StartAsyncTests
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

    [TestMethod]
    public async Task CreatesConnectionAndCallsSubscribe()
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

        var store = BuildStoreWithSubscription();
        await using var sp = BuildServiceProvider(store);

        var subscriber = new RabbitMqSubscriber(sp, new Mock<IProxy>().Object, mockFactory.Object, NullLogger<RabbitMqSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);

        mockFactory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockConnection.Verify(c => c.CreateChannelAsync(), Times.Once);
    }
}
