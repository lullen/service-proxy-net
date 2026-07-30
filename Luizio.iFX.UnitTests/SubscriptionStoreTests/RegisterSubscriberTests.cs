using Luizio.iFX.Messaging;
using Luizio.iFX.UnitTests.TestDoubles;

namespace Luizio.iFX.UnitTests.SubscriptionStoreTests;

[TestClass]
public class RegisterSubscriberTests
{
    private static SubscriberSettings Settings(bool useDeadLetterQueue = false)
        => new() { RetryCount = 3, PrefetchCount = 1, UseDeadLetterQueue = useDeadLetterQueue, PubSub = "pubsub" };

    private static readonly Type[] BothTransitionEvents =
        [typeof(TestTransitionStartedEvent), typeof(TestTransitionFinishedEvent)];

    [TestMethod]
    public void ConcreteSubscription_BindsToItsOwnExchange()
    {
        var store = new SubscriptionStore();

        store.RegisterSubscriber<TestSubscriberService>(x => x.Handle, Settings());

        var subscription = store.GetSubscriptions().Single();
        CollectionAssert.AreEqual(new[] { typeof(TestEvent).FullName! }, subscription.BoundExchanges.ToArray());
        Assert.AreEqual(typeof(TestEvent).FullName, subscription.QueueTopic);
        Assert.AreEqual(typeof(TestEvent), subscription.TypesByExchange[typeof(TestEvent).FullName!]);
    }

    [TestMethod]
    public void InterfaceSubscription_BindsToEveryDeclaredType()
    {
        var store = new SubscriptionStore();

        store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), BothTransitionEvents);

        var subscription = store.GetSubscriptions().Single();
        CollectionAssert.AreEqual(
            new[] { typeof(TestTransitionStartedEvent).FullName!, typeof(TestTransitionFinishedEvent).FullName! },
            subscription.BoundExchanges.ToArray());
        Assert.AreEqual(typeof(TestTransitionStartedEvent), subscription.TypesByExchange[typeof(TestTransitionStartedEvent).FullName!]);
        Assert.AreEqual(typeof(TestTransitionFinishedEvent), subscription.TypesByExchange[typeof(TestTransitionFinishedEvent).FullName!]);
    }

    [TestMethod]
    public void InterfaceSubscription_NamesQueueAndDlqAfterTheParameterType()
    {
        var store = new SubscriptionStore();

        store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(useDeadLetterQueue: true), BothTransitionEvents);

        var subscription = store.GetSubscriptions().Single();
        Assert.AreEqual(typeof(ITestTransitionEvent).FullName, subscription.QueueTopic);
        Assert.AreEqual($"{typeof(ITestTransitionEvent).FullName}_dlq", subscription.DeadLetterQueue);
    }

    [TestMethod]
    public void InterfaceSubscription_BuildsAnInvoker()
    {
        // Covers the generic constraint: MakeGenericMethod rejects an interface against `new()`.
        var store = new SubscriptionStore();

        store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), BothTransitionEvents);

        Assert.IsNotNull(store.GetSubscriptions().Single().Invoker);
    }

    [TestMethod]
    public void Throws_WhenParameterIsAnInterfaceAndBindToIsOmitted()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings()));

        StringAssert.Contains(e.Message, "not a concrete event type");
    }

    [TestMethod]
    public void Throws_WhenBindToIsEmpty()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), []));

        StringAssert.Contains(e.Message, "empty");
    }

    [TestMethod]
    public void Throws_WhenBindToContainsAnAbstractType()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(),
                [typeof(TestTransitionStartedEvent), typeof(TestAbstractEvent)]));

        StringAssert.Contains(e.Message, "non-abstract class");
    }

    [TestMethod]
    public void Throws_WhenBindToContainsATypeTheHandlerCannotAccept()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(),
                [typeof(TestTransitionStartedEvent), typeof(TestUnrelatedEvent)]));

        StringAssert.Contains(e.Message, "not assignable");
    }

    [TestMethod]
    public void Throws_WhenBindToListsTheSameTypeTwice()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(),
                [typeof(TestTransitionStartedEvent), typeof(TestTransitionStartedEvent)]));

        StringAssert.Contains(e.Message, "more than once");
    }

    [TestMethod]
    public void Throws_WhenTheEventTypeIsNotAnIEvent()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestNonEventSubscriberService>(x => x.Handle, Settings()));

        StringAssert.Contains(e.Message, "must implement IEvent");
    }

    [TestMethod]
    public void Throws_WhenABindToTypeIsNotAnIEvent()
    {
        var store = new SubscriptionStore();

        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestNonEventSubscriberService>(x => x.OnSomething, Settings(),
                [typeof(NotAnEventImplementation)]));

        StringAssert.Contains(e.Message, "must implement IEvent");
    }

    [TestMethod]
    public void Throws_WhenTwoSubscriptionsWouldShareAQueue()
    {
        var store = new SubscriptionStore();
        store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), BothTransitionEvents);

        // Same service and method, so the same queue name — competing consumers, and each message
        // would reach only one of the two handlers.
        var e = Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(),
                [typeof(TestTransitionStartedEvent)]));

        StringAssert.Contains(e.Message, "is already registered");
        Assert.AreEqual(1, store.GetSubscriptions().Count());
    }

    [TestMethod]
    public void AllowsTwoSubscriptionsOnTheSameServiceWithDifferentMethods()
    {
        var store = new SubscriptionStore();

        store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), BothTransitionEvents);
        store.RegisterSubscriber<TestSubscriberService>(x => x.Handle, Settings());

        Assert.AreEqual(2, store.GetSubscriptions().Count());
    }

    [TestMethod]
    public void NoSubscriptionIsRegistered_WhenValidationFails()
    {
        var store = new SubscriptionStore();

        Assert.ThrowsExactly<ArgumentException>(() =>
            store.RegisterSubscriber<TestTransitionSubscriberService>(x => x.OnTransition, Settings(), []));

        Assert.AreEqual(0, store.GetSubscriptions().Count());
    }
}
