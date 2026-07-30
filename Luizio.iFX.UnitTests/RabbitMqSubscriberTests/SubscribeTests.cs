using Luizio.iFX.Client;
using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
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

    private static Subscription BuildSubscription(ushort prefetchCount = 0, string? deadLetterQueue = null)
    {
        return new Subscription
        {
            Invoker = (sp, cu, msg) => Task.FromResult(Error.Empty),
            EventType = typeof(TestEvent),
            MethodName = nameof(TestSubscriberService.Handle),
            Service = typeof(TestSubscriberService).Name.ToLower(),
            QueueTopic = typeof(TestEvent).FullName!,
            BoundExchanges = [typeof(TestEvent).FullName!],
            TypesByExchange = new Dictionary<string, Type> { [typeof(TestEvent).FullName!] = typeof(TestEvent) },
            DeadLetterQueue = deadLetterQueue,
            RetryCount = 3,
            PrefetchCount = prefetchCount
        };
    }

    private static Subscription BuildInterfaceSubscription()
    {
        return new Subscription
        {
            Invoker = (sp, cu, msg) => Task.FromResult(Error.Empty),
            EventType = typeof(ITestTransitionEvent),
            MethodName = nameof(TestTransitionSubscriberService.OnTransition),
            Service = typeof(TestTransitionSubscriberService).Name.ToLower(),
            QueueTopic = typeof(ITestTransitionEvent).FullName!,
            BoundExchanges = [typeof(TestTransitionStartedEvent).FullName!, typeof(TestTransitionFinishedEvent).FullName!],
            TypesByExchange = new Dictionary<string, Type>
            {
                [typeof(TestTransitionStartedEvent).FullName!] = typeof(TestTransitionStartedEvent),
                [typeof(TestTransitionFinishedEvent).FullName!] = typeof(TestTransitionFinishedEvent)
            },
            RetryCount = 3
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
        mockChannel.Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
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

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueDeclareAsync(subscription.QueueName, true, false, false, null), Times.Once);
    }

    [TestMethod]
    public async Task BindsQueueToSubscriptionTopic()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueBindAsync(subscription.QueueName, subscription.QueueTopic, string.Empty), Times.Once);
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

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.BasicConsumeAsync(subscription.QueueName, false, It.IsAny<IRabbitMqConsumer>()), Times.Once);
    }

    [TestMethod]
    public async Task DeclaresEachBoundExchange_BeforeBinding()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildInterfaceSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        foreach (var exchange in subscription.BoundExchanges)
            mockChannel.Verify(c => c.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, true), Times.Once);
    }

    [TestMethod]
    public async Task BindsOneQueueToEveryBoundExchange()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildInterfaceSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueDeclareAsync(subscription.QueueName, true, false, false, It.IsAny<IDictionary<string, object?>>()), Times.Once);
        foreach (var exchange in subscription.BoundExchanges)
            mockChannel.Verify(c => c.QueueBindAsync(subscription.QueueName, exchange, string.Empty), Times.Once);
    }

    [TestMethod]
    public async Task QueueIsNamedAfterTheParameterType_NotTheBoundExchanges()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildInterfaceSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);
        var expected = $"{typeof(ITestTransitionEvent).FullName}_{typeof(TestTransitionSubscriberService).Name.ToLower()}_ontransition";

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        Assert.AreEqual(expected, subscription.QueueName);
        mockChannel.Verify(c => c.BasicConsumeAsync(expected, false, It.IsAny<IRabbitMqConsumer>()), Times.Once);
    }

    [TestMethod]
    public async Task DeclaresDeadLetterQueueAndWiresQueueArguments_WhenDeadLetterQueueIsSet()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var dlq = $"{typeof(TestEvent).FullName}_dlq";
        var subscription = BuildSubscription(deadLetterQueue: dlq);
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueDeclareAsync(dlq, true, false, false, null), Times.Once);
        mockChannel.Verify(c => c.QueueDeclareAsync(
            subscription.QueueName, true, false, false,
            It.Is<IDictionary<string, object?>>(a =>
                (string)a["x-dead-letter-exchange"]! == string.Empty &&
                (string)a["x-dead-letter-routing-key"]! == dlq)),
            Times.Once);
    }

    [TestMethod]
    public async Task PassesNoQueueArguments_WhenDeadLetterQueueIsNotSet()
    {
        var (mockConnection, mockChannel) = BuildMocks();
        var subscription = BuildSubscription();
        var store = BuildStoreWith(subscription);
        await using var sp = BuildServiceProvider(store);

        await BuildSubscriber(sp).Subscribe(mockConnection.Object);

        mockChannel.Verify(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), null), Times.Once);
    }
}
