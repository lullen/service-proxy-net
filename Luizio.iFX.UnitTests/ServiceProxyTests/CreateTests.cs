using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.iFX.UnitTests.ServiceProxyTests;

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
    public void Create_ReturnsNonNullProxy()
    {
        using var sp = BuildServiceProvider();

        var proxy = ServiceProxy<ITestProxyService>.Create(ProxyType.InProc, sp, "app", "ITestProxyService");

        Assert.IsNotNull(proxy);
    }

    [TestMethod]
    public void Create_ReturnsInstanceImplementingInterface()
    {
        using var sp = BuildServiceProvider();

        var proxy = ServiceProxy<ITestProxyService>.Create(ProxyType.InProc, sp, "app", "ITestProxyService");

        Assert.IsInstanceOfType<ITestProxyService>(proxy);
    }
}
