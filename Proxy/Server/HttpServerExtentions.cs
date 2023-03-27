using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Client;
using Proxy.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy.Server
{
    public static class HttpServerExtentions
    {

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
                    var parameter = await context.Request.ReadFromJsonAsync(firstParam.ParameterType);
                    if (parameter is null)
                    {
                        context.Response.StatusCode = 400;
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

        private static void InitCurrentUser(CurrentUser currentUser, HttpContext context)
        {
            currentUser.Metadata = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value);
        }
    }
}
