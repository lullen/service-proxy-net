using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.iFX.UnitTests.ServiceProxyTests;

[TestClass]
public class InvokeTests
{
    private static ServiceProvider BuildServiceProvider(Action<ServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUser>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task ReturnsSuccessResponse_WhenServiceAndMethodExist()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<ITestProxyService, TestProxyServiceImpl>("itestproxyservice"));

        var proxy = ServiceProxy<ITestProxyService>.Create(ProxyType.InProc, sp, "app", "ITestProxyService");

        var result = await proxy.Handle(new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.AreEqual("proxy-ok", result.Result?.Value);
    }

    [TestMethod]
    public async Task ThrowsInvalidOperationException_WhenReturnTypeIsWrong()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<IWrongReturnProxyService, WrongReturnProxyServiceImpl>("iwrongreturnproxyservice"));

        var proxy = ServiceProxy<IWrongReturnProxyService>.Create(ProxyType.InProc, sp, "app", "IWrongReturnProxyService");

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.Handle(new TestRequest()));
    }

    [TestMethod]
    public async Task ThrowsInvalidOperationException_WhenArgIsNull()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<ITestProxyService, TestProxyServiceImpl>("itestproxyservice"));

        var proxy = ServiceProxy<ITestProxyService>.Create(ProxyType.InProc, sp, "app", "ITestProxyService");

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.Handle(null!));
    }

    [TestMethod]
    public async Task ThrowsInvalidOperationException_WhenMethodHasNoArgs()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<INoArgProxyService, NoArgProxyServiceImpl>("inoargproxyservice"));

        var proxy = ServiceProxy<INoArgProxyService>.Create(ProxyType.InProc, sp, "app", "INoArgProxyService");

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.Handle());
    }
}
