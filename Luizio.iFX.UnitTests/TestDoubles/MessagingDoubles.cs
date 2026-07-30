using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.Server;

namespace Luizio.iFX.UnitTests.TestDoubles;

public class TestEvent : IEvent { }

public class TestSubscriberService : IService
{
    public Task<Response<Empty>> Handle(TestEvent @event)
        => Task.FromResult<Response<Empty>>(new Empty());

    public Task<Response<Empty>> FailingHandle(TestEvent @event)
        => Task.FromResult<Response<Empty>>(new Error(ErrorCode.Error, "failed"));
}

/// <summary>An interface several concrete event types share, as an interface subscription binds to.</summary>
public interface ITestTransitionEvent : IEvent
{
    Guid Id { get; set; }
}

public class TestTransitionStartedEvent : ITestTransitionEvent
{
    public Guid Id { get; set; }
    public string StartedBy { get; set; } = string.Empty;
}

public class TestTransitionFinishedEvent : ITestTransitionEvent
{
    public Guid Id { get; set; }
}

public abstract class TestAbstractEvent : ITestTransitionEvent
{
    public Guid Id { get; set; }
}

/// <summary>An IEvent that does not implement <see cref="ITestTransitionEvent"/>.</summary>
public class TestUnrelatedEvent : IEvent { }

/// <summary>A concrete type that is not an <see cref="IEvent"/> and so can never be published.</summary>
public class NotAnEvent { }

/// <summary>An interface that does not derive from <see cref="IEvent"/>.</summary>
public interface INotAnEventInterface
{
    Guid Id { get; set; }
}

public class NotAnEventImplementation : INotAnEventInterface
{
    public Guid Id { get; set; }
}

public class TestNonEventSubscriberService : IService
{
    public Task<Response<Empty>> Handle(NotAnEvent message)
        => Task.FromResult<Response<Empty>>(new Empty());

    public Task<Response<Empty>> OnSomething(INotAnEventInterface message)
        => Task.FromResult<Response<Empty>>(new Empty());
}

public class TestTransitionSubscriberService : IService
{
    public List<ITestTransitionEvent> Received { get; } = [];

    public Task<Response<Empty>> OnTransition(ITestTransitionEvent @event)
    {
        Received.Add(@event);
        return Task.FromResult<Response<Empty>>(new Empty());
    }
}
