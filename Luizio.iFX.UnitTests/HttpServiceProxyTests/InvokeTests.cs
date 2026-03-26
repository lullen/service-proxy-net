using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Luizio.iFX.UnitTests.HttpServiceProxyTests;

// HttpServiceProxy uses a private static readonly HttpClient. Tests replace it
// via UnsafeAccessor (available since .NET 8), which obtains a ref to the field
// and bypasses the initonly restriction that blocks reflection in .NET 10.
// [DoNotParallelize] ensures the static field is not mutated by concurrent tests.
[TestClass]
[DoNotParallelize]
public class InvokeTests
{
    // UnsafeAccessor for static fields: the instance parameter is unused (pass null).
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "http")]
    private static extern ref HttpClient GetHttpField(HttpServiceProxy? _);

    private static HttpClient _originalClient = null!;
    private Mock<HttpMessageHandler> _mockHandler = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _originalClient = GetHttpField(null);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        GetHttpField(null) = _originalClient;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        GetHttpField(null) = new HttpClient(_mockHandler.Object);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string body)
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
    }

    private void SetupHttpResponseWithCapture(HttpStatusCode statusCode, string body, Action<HttpRequestMessage> capture)
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage req, CancellationToken _) =>
            {
                capture(req);
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body)
                });
            });
    }

    private static ServiceProvider BuildServiceProvider(string appName, string baseUrl)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUser>();
        services.AddSingleton<IOptions<ServiceSettings>>(
            Options.Create(new ServiceSettings { Services = { [appName] = baseUrl } }));
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task ReturnsDeserializedResponse_WhenServerReturnsSuccess()
    {
        var body = JsonSerializer.Serialize(new TestResponse { Value = "hello" });
        SetupHttpResponse(HttpStatusCode.OK, body);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.AreEqual("hello", result.Result?.Value);
    }

    [TestMethod]
    public async Task ReturnsExceptionResponse_WhenAppNameNotConfigured()
    {
        await using var sp = BuildServiceProvider("RegisteredApp", "http://localhost");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "UnknownApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsTrue(result.HasError);
        Assert.AreEqual(ErrorCode.Exception, result.Error.Code);
        StringAssert.Contains(result.Error.Description, "UnknownApp");
    }

    [TestMethod]
    public async Task ReturnsExceptionResponse_WhenServerReturnsNonSuccessStatus()
    {
        SetupHttpResponse(HttpStatusCode.InternalServerError, string.Empty);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsTrue(result.HasError);
        Assert.AreEqual(ErrorCode.Exception, result.Error.Code);
    }

    [TestMethod]
    public async Task PostsToCorrectUrl_FromAppServiceAndMethodName()
    {
        HttpRequestMessage? captured = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, "{}", req => captured = req);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost:5000");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Post, captured.Method);
        Assert.AreEqual("http://localhost:5000/MyService/MyMethod", captured.RequestUri?.ToString());
    }

    [TestMethod]
    public async Task TrimsTrailingSlashFromBaseUrl()
    {
        HttpRequestMessage? captured = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, "{}", req => captured = req);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost:5000/");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsNotNull(captured);
        Assert.AreEqual("http://localhost:5000/MyService/MyMethod", captured.RequestUri?.ToString());
    }

    [TestMethod]
    public async Task SendsCurrentUserMetadataAsHeaders()
    {
        HttpRequestMessage? captured = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, "{}", req => captured = req);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost");
        var user = new CurrentUser { Token = "Bearer token123" };
        user.Metadata.Add(new KeyValuePair<string, string>("X-Tenant-Id", "tenant-abc"));

        var proxy = new HttpServiceProxy(sp, user);

        await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsNotNull(captured);
        Assert.IsTrue(captured.Headers.Contains("Authorization"));
        Assert.IsTrue(captured.Headers.Contains("X-Tenant-Id"));
        Assert.AreEqual("tenant-abc", captured.Headers.GetValues("X-Tenant-Id").First());
    }

    [TestMethod]
    public async Task SendsMultipartRequest_WhenRequestHasStreamProperty()
    {
        HttpRequestMessage? captured = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, "{}", req => captured = req);

        await using var sp = BuildServiceProvider("MyApp", "http://localhost");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        var request = new StreamRequest
        {
            File = new MemoryStream([1, 2, 3]),
            FileName = "test.bin"
        };

        await proxy.Invoke<StreamRequest, TestResponse>(
            "MyApp", "MyService", "Upload", request);

        Assert.IsNotNull(captured);
        Assert.IsInstanceOfType<MultipartFormDataContent>(captured.Content);
    }

    [TestMethod]
    public async Task ReturnsDefaultInstance_WhenServerReturnsNullJson()
    {
        SetupHttpResponse(HttpStatusCode.OK, "null");

        await using var sp = BuildServiceProvider("MyApp", "http://localhost");
        var proxy = new HttpServiceProxy(sp, new CurrentUser());

        var result = await proxy.Invoke<TestRequest, TestResponse>(
            "MyApp", "MyService", "MyMethod", new TestRequest());

        Assert.IsFalse(result.HasError);
        Assert.IsNotNull(result.Result);
    }
}
