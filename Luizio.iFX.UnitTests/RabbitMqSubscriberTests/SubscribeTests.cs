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
public class SubscribeTests
{
    private static ServiceProvider BuildServiceProvider(SubscriptionStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUser>();
        services.AddSingleton(store);
        return services.BuildServiceProvider();
    }

    private static Subscription BuildSubscription(ushort prefetchCount = 0)
    {
        return new Subscription
        {
            ServiceType = typeof(TestSubscriberService),
            Service = typeof(TestSubscriberService).Name.ToLower(),
            Method = typeof(TestSubscriberService).GetMethod(nameof(TestSubscriberService.Handle))!,
            Topic = typeof(TestEvent).FullName!,
            RetryCount = 3,
            PrefetchCount = prefetchCount
        };
    }

    private static SubscriptionStore BuildStoreWith(Subscription subscription)
    {
        var store = new SubscriptionStore();
        var field = typeof(SubscriptionStore).GetField("_subscriptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var list = (IList<Subscription>)field.GetValue(store)!;
        list.Add(subscription);
        return store;
    }

    private static (Mock<IRabbitMqConnection>, Mock<IRabbitMqChannel>) BuildMocks()
    {
        var mockConnection = new Mock<IRabbitMqConnection>();
        var mockChannel = new Mock<IRabbitMqChannel>();

        mockConnection.Setup(c => c.CreateChannelAsync()).ReturnsAsync(mockChannel.Object);
        mockChannel.Setup(c => c.CreateConsumer()).Returns(new Mock<IRabbitMqConsumer>().Object);
        mockChannel.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object?>>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.BasicQosAsync(It.IsAny<uint>(), It.IsAny<ushort>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        mockChannel.Setup(c => c.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IRabbitMqConsumer>()))
            .ReturnsAsync("consumer-tag");

        return (mockConnection, mockChannel);
    }

    private static RabbitMqSubscriber BuildSubscriber(ServiceProvider sp)
        => new(sp, new Mock<IRabbitMqConnectionFactory>().Object, NullLogger<RabbitMqSubscriber>.Instance);

    [TestMethod]
    public async Task CreatesChannelForEachSubscription()
    {
        var (mockConnection, _) = BuildMocks();
        var store = BuildStoreWith(BuildSubscription());
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockConnection.Verify(c => c.CreateChannelAsync(), Times.Once);
    }

    [TestMethod]
    public async Task DeclaresQueueWithCorrectName()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);
        var expectedQueueName = $"{subscription.Topic}_{subscription.Service}_{subscription.Method!.Name.ToLower()}";

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueDeclareAsync(expectedQueueName, true, false, false, null), Times.Once);
    }

    [TestMethod]
    public async Task BindsQueueToSubscriptionTopic()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);
        var expectedQueueName = $"{subscription.Topic}_{subscription.Service}_{subscription.Method!.Name.ToLower()}";

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueBindAsync(expectedQueueName, subscription.Topic, string.Empty), Times.Once);
    }

    [TestMethod]
    public async Task SetsBasicQos_WhenPrefetchCountIsAboveZero()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        const ushort prefetchCount = 5;
        var store = BuildStoreWith(BuildSubscription(prefetchCount));
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.BasicQosAsync(0, prefetchCount, false), Times.Once);
    }

    [TestMethod]
    public async Task DoesNotSetBasicQos_WhenPrefetchCountIsZero()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var store = BuildStoreWith(BuildSubscription(prefetchCount: 0));
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.BasicQosAsync(It.IsAny<uint>(), It.IsAny<ushort>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task StartsConsuming_WithCorrectQueueName()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);
        var expectedQueueName = $"{subscription.Topic}_{subscription.Service}_{subscription.Method!.Name.ToLower()}";

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.BasicConsumeAsync(expectedQueueName, false, It.IsAny<IRabbitMqConsumer>()), Times.Once);
    }
}
