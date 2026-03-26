using Luizio.iFX.Models;
using Luizio.iFX.Server;

namespace Luizio.iFX.UnitTests.TestDoubles;

public interface ITestProxyService : IService
{
    Task<Response<TestResponse>> Handle(TestRequest request);
}

public class TestProxyServiceImpl : ITestProxyService
{
    public Task<Response<TestResponse>> Handle(TestRequest request)
        => Task.FromResult<Response<TestResponse>>(new TestResponse { Value = "proxy-ok" });
}

public interface INoArgProxyService : IService
{
    Task<Response<TestResponse>> Handle();
}

public class NoArgProxyServiceImpl : INoArgProxyService
{
    public Task<Response<TestResponse>> Handle()
        => Task.FromResult<Response<TestResponse>>(new TestResponse { Value = "no-arg" });
}

public interface IWrongReturnProxyService : IService
{
    Task<List<TestResponse>> Handle(TestRequest request);
}

public class WrongReturnProxyServiceImpl : IWrongReturnProxyService
{
    public Task<List<TestResponse>> Handle(TestRequest request)
        => Task.FromResult(new List<TestResponse>());
}
