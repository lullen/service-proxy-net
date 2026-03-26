using System.Net;
using System.Net.Http.Json;
using Luizio.iFX.Models;
using Luizio.iFX.Server;
using Luizio.iFX.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Luizio.iFX.UnitTests.HttpServerExtensionsTests;

[TestClass]
public class MapServiceTests
{
    private static async Task<(WebApplication app, HttpClient client)> BuildTestApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<TestService>();
        var app = builder.Build();
        app.MapService<TestService>();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [TestMethod]
    public async Task ReturnsWebApplication_ForFluentChaining()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<TestService>();
        var app = builder.Build();

        var result = app.MapService<TestService>();

        Assert.AreSame(app, result);
        await app.StopAsync();
    }

    [TestMethod]
    public async Task ReturnsBadRequest_WhenContentTypeIsNull()
    {
        var (app, client) = await BuildTestApp();
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/TestService/Handle");
            // no Content-Type set, no body
            var response = await client.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [TestMethod]
    public async Task ReturnsOk_WhenPostWithJsonBody()
    {
        var (app, client) = await BuildTestApp();
        try
        {
            var response = await client.PostAsJsonAsync("/TestService/Handle", new TestRequest());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TestResponse>();
            Assert.AreEqual("ok", result?.Value);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [TestMethod]
    public async Task ReturnsBadRequest_WhenBodyIsNull()
    {
        var (app, client) = await BuildTestApp();
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/TestService/Handle")
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            };
            var response = await client.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
