using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.iFX.UnitTests.ProxyTests;

[TestClass]
public class CreateTests
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
    public void Create_ReturnsInstanceImplementingInterface()
    {
        using var sp = BuildServiceProvider();
        var proxy = new Proxy(sp, ProxyType.InProc);

        var result = proxy.Create<ITestProxyService>("app", "ITestProxyService");

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ITestProxyService>(result);
    }

    [TestMethod]
    public async Task Create_UsesDefaultProxyType_WhenNoExplicitTypeGiven()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<ITestProxyService, TestProxyServiceImpl>("itestproxyservice"));

        var proxy = new Proxy(sp, ProxyType.InProc);

        var result = await proxy.Create<ITestProxyService>("app", "ITestProxyService").Handle(new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.AreEqual("proxy-ok", result.Result?.Value);
    }

    [TestMethod]
    public async Task Create_WithExplicitProxyType_OverridesDefault()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<ITestProxyService, TestProxyServiceImpl>("itestproxyservice"));

        // Default is HTTP (would fail without a server), but explicit override to InProc succeeds
        var proxy = new Proxy(sp, ProxyType.HTTP);

        var result = await proxy.Create<ITestProxyService>("app", "ITestProxyService", ProxyType.InProc).Handle(new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.AreEqual("proxy-ok", result.Result?.Value);
    }
}
