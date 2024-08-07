
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public class HttpServiceProxy : IServiceProxy
{
    private readonly ServiceSettings serviceSettings;
    private readonly IServiceProvider sp;
    private readonly CurrentUser currentUser;
    private static readonly HttpClient http = new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; } });
    public HttpServiceProxy(IServiceProvider sp, CurrentUser currentUser)
    {
        this.sp = sp;
        this.currentUser = currentUser;
        using var scope = sp.CreateScope();
        this.serviceSettings = scope.ServiceProvider.GetRequiredService<IOptions<ServiceSettings>>().Value;
    }

    public async Task<Response<TRes>> Invoke<T, TRes>(string appName, string serviceName, string methodName, T requestData)
        where T : class, new()
        where TRes : class
    {
        using var scope = sp.CreateScope();
        try
        {
            if (!serviceSettings.Services.TryGetValue(appName, out var baseUrl))
            {
                throw new InvalidOperationException($"Application url for {appName} is not configured.");
            }

            baseUrl = baseUrl.TrimEnd('/');
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
            var responseMessage = await http.SendAsync(requestMessage);
            responseMessage.EnsureSuccessStatusCode();

            var text = await responseMessage.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<TRes>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (response == null && !typeof(TRes).IsAbstract)
            {
                return typeof(TRes).GetConstructor(Type.EmptyTypes).Invoke(null) as TRes;
            }
            return response;
        }
        catch (Exception e)
        {
            var error = new Error(ErrorCode.Exception, e.ToString());
            return error;
        }

    }

    private static bool HasStreamAsProperty(Type type)
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

    private static HttpRequestMessage CreateMultipartRequest<T>(string url, T data, List<KeyValuePair<string, string>> headers)
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

    private static Stream GetStreamFromProperty(Type type, object value)
    {
        var property = type.GetProperties().SingleOrDefault(p => p.PropertyType == typeof(Stream));
        return (property!.GetValue(value) as Stream)!;
    }


    private static HttpRequestMessage CreateRequest<T>(string url, T data, List<KeyValuePair<string, string>> headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(data)
        };

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        return request;
    }
}
