
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public class HttpServiceProxy : IServiceProxy
{
    private readonly ServiceSettings serviceSettings;
    private readonly IServiceProvider sp;
    private readonly CurrentUser currentUser;

    public HttpServiceProxy(IServiceProvider sp, CurrentUser currentUser)
    {
        this.sp = sp;
        this.currentUser = currentUser;
        using var scope = sp.CreateScope();
        this.serviceSettings = scope.ServiceProvider.GetRequiredService<IOptions<ServiceSettings>>().Value;
    }

    public async Task<Response<TRes>> Invoke<T, TRes>(string appName, string serviceName, string methodName, T requestData)
        where T : class, new()
        where TRes : class, new()
    {
        using var scope = sp.CreateScope();
        try
        {
            if (!serviceSettings.Services.TryGetValue(appName, out var baseUrl))
            {
                throw new InvalidOperationException($"Application {appName} is not configured.");
            }
            var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

            var url = $"{baseUrl}/{serviceName}/{methodName}";

            HttpRequestMessage requestMessage;

            if (HasStreamAsProperty(requestData.GetType()))
            {
                requestMessage = CreateMultipartRequest(url, requestData, currentUser.Metadata);
            }
            else
            {
                requestMessage = CreateRequest(url, requestData, currentUser.Metadata);
            }
            var responseMessage = await httpClient.SendAsync(requestMessage);
            responseMessage.EnsureSuccessStatusCode();

            var text = await responseMessage.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<TRes>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new Response<TRes>(response);
        }
        catch (Exception e)
        {
            var error = new Error(ErrorCode.Exception, e.ToString());
            //var genericType = typeof(Response<>).MakeGenericType(typeof(TRes));
            var objectResponse = Activator.CreateInstance(typeof(Response<TRes>), new object[] { error });
            return objectResponse as Response<TRes>;
        }

    }

    private bool HasStreamAsProperty(Type type)
    {
        foreach (var property in type.GetProperties())
        {
            if (property.PropertyType == typeof(System.IO.Stream))
            {
                return true;
            }
        }
        return false;
    }

    private HttpRequestMessage CreateMultipartRequest<T>(string url, T data, Dictionary<string, StringValues> headers)
    {
        var stream = GetStreamFromProperty(data.GetType(), data);

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var stringContent = new StringContent(JsonSerializer.Serialize(data));
        var streamContent = new StreamContent(stream);
        var content = new MultipartFormDataContent
        {
            { stringContent, "data" },
            { streamContent, "file", "file.file" }
        };
        request.Content = content;
        return request;
    }

    private Stream GetStreamFromProperty(Type type, object value)
    {
        var property = type.GetProperties().SingleOrDefault(p => p.PropertyType == typeof(Stream));
        return (property!.GetValue(value) as Stream)!;
    }


    private HttpRequestMessage CreateRequest<T>(string url, T data, Dictionary<string, StringValues> headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(data)
        };

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value.ToArray());
        }
        return request;
    }
}
