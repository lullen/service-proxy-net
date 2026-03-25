using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Luizio.iFX.UnitTests.InProcServiceProxyTests;

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
            s.AddKeyedScoped<TestService>("testservice"));

        var proxy = new InProcServiceProxy<TestService>(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "app", "TestService", "Handle", new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.IsNotNull(result.Result);
        Assert.AreEqual("ok", result.Result.Value);
    }

    [TestMethod]
    public async Task ReturnsExceptionResponse_WhenMethodThrows()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<TestService>("testservice"));

        var proxy = new InProcServiceProxy<TestService>(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "app", "TestService", "ThrowingMethod", new TestRequest());

        Assert.IsTrue(result.HasError);
        Assert.AreEqual(ErrorCode.Exception, result.Error.Code);
        StringAssert.Contains(result.Error.Description, "method failed");
    }

    [TestMethod]
    public async Task ThrowsInvalidOperationException_WhenServiceNotRegistered()
    {
        await using var sp = BuildServiceProvider(); // TestService not registered

        var proxy = new InProcServiceProxy<TestService>(sp, new CurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proxy.Invoke<TestRequest, TestResponse>(
                "app", "TestService", "Handle", new TestRequest()));
    }

    [TestMethod]
    public async Task ThrowsException_WhenMethodNameNotFound()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<TestService>("testservice"));

        var proxy = new InProcServiceProxy<TestService>(sp, new CurrentUser());

        await Assert.ThrowsAsync<Exception>(() =>
            proxy.Invoke<TestRequest, TestResponse>(
                "app", "TestService", "NonExistentMethod", new TestRequest()));
    }

    [TestMethod]
    public async Task PropagatesCurrentUserTokenToScopedCurrentUser()
    {
        var capture = new CurrentUserCapture();

        await using var sp = BuildServiceProvider(s =>
        {
            s.AddSingleton(capture);
            s.AddKeyedScoped<UserCapturingTestService>("userservice");
        });

        var originalUser = new CurrentUser { Token = "Bearer abc123" };
        var proxy = new InProcServiceProxy<UserCapturingTestService>(sp, originalUser);

        await proxy.Invoke<TestRequest, TestResponse>(
            "app", "UserService", "Handle", new TestRequest());

        Assert.AreEqual("Bearer abc123", capture.CapturedToken);
    }

    [TestMethod]
    public async Task PropagatesCurrentUserMetadataToScopedCurrentUser()
    {
        var capture = new CurrentUserCapture();

        await using var sp = BuildServiceProvider(s =>
        {
            s.AddSingleton(capture);
            s.AddKeyedScoped<UserCapturingTestService>("userservice");
        });

        var originalUser = new CurrentUser();
        originalUser.Metadata.Add(new KeyValuePair<string, string>("custom-key", "custom-value"));

        var proxy = new InProcServiceProxy<UserCapturingTestService>(sp, originalUser);

        await proxy.Invoke<TestRequest, TestResponse>(
            "app", "UserService", "Handle", new TestRequest());

        Assert.IsTrue(capture.CapturedMetadata.Any(m => m.Key == "custom-key" && m.Value == "custom-value"));
    }

    [TestMethod]
    public async Task IsCaseInsensitive_WhenResolvingMethodName()
    {
        await using var sp = BuildServiceProvider(s =>
            s.AddKeyedScoped<TestService>("testservice"));

        var proxy = new InProcServiceProxy<TestService>(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "app", "TestService", "HANDLE", new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.AreEqual("ok", result.Result?.Value);
    }
}
