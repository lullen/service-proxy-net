using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Proxy.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Proxy.Server;

public static class HttpServerExtentions
{
    private const string MultipartContentType = "multipart/form-data";
    public static WebApplication MapService<T>(this WebApplication app) where T : class, IService
    {
        var type = typeof(T);
        var methods = type.GetMethods();

        foreach (var method in methods)
        {
            app.MapPost($"{type.Name}/{method.Name}", async (HttpContext context, IServiceProvider serviceProvider) =>
            {
                var params1 = method.GetParameters();
                var firstParam = params1.First();
                object? parameter = null;
                if (context.Request.ContentType is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                if (context.Request.ContentType.Contains(MultipartContentType))
                {
                    parameter = ReadFromForm(context, firstParam.ParameterType);
                }
                else
                {
                    parameter = await ReadFromJson(context, firstParam.ParameterType);
                }


                if (parameter is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                InitCurrentUser(serviceProvider.GetRequiredService<CurrentUser>(), context);

                var service = serviceProvider.GetRequiredService<T>();

                var task = (Task)method.Invoke(service, new[] { parameter });
                await task!.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                var result = resultProperty!.GetValue(task);
                var res = result.GetType().GetProperty("Result").GetValue(result);

                var responseType = method.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
                await context.Response.WriteAsJsonAsync(res, responseType);
            });
        }
        return app;
    }

    private static object? ReadFromForm(HttpContext context, Type parameterType)
    {
        var data = context.Request.Form["data"].ToString() ?? "";
        var parameter = JsonSerializer.Deserialize(data, parameterType);

        var streamProperty = parameterType.GetProperties().SingleOrDefault(p => p.PropertyType == typeof(Stream)) ?? throw new Exception("No stream found in properties");
        streamProperty.SetValue(parameter, context.Request.Form.Files[0].OpenReadStream());

        return parameter;
    }

    private static async Task<object?> ReadFromJson(HttpContext context, Type parameterType)
    {
        return await context.Request.ReadFromJsonAsync(parameterType);
    }

    private static void InitCurrentUser(CurrentUser currentUser, HttpContext context)
    {
        currentUser.Metadata = context.User.Claims.Select(c => KeyValuePair.Create<string, StringValues>(c.Type, c.Value)).ToDictionary(c => c.Key, c => c.Value);
        if (context.Request.Headers.TryGetValue("Authorization", out var token))
        {
            currentUser.Token = token.ToString();
        }
    }
}
