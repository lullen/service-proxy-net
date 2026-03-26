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
