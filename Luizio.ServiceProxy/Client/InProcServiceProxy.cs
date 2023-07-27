
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.Logging;

namespace Luizio.ServiceProxy.Client;

public class InProcServiceProxy : IServiceProxy
{
    private readonly IServiceProvider sp;
    private readonly CurrentUser currentUser;

    public InProcServiceProxy(IServiceProvider sp, CurrentUser currentUser)
    {
        this.sp = sp;
        this.currentUser = currentUser;
    }

    public async Task<Response<TRes>> Invoke<T, TRes>(string appName, string serviceName, string methodName, T request)
        where T : class, new()
        where TRes : class, new()
    {
        using var scope = sp.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InProcServiceProxy>>();
        logger.LogInformation("Calling {AppName}.{ServiceName}.{MethodName}", appName, serviceName, methodName);
        var newCurrentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
        newCurrentUser.Metadata = currentUser.Metadata;
        newCurrentUser.Token = currentUser.Token;

        var serviceImpl = ServiceStore.GetService(serviceName, scope.ServiceProvider);
        var methodToInvoke = ServiceStore.GetMethod(methodName, serviceImpl);
        try
        {
            var res = methodToInvoke.Invoke(serviceImpl, new[] { request });
            var task = res as Task<Response<TRes>>;
            var response = await task!;

            if (response.HasError)
            {
                logger.LogError("Method call to {AppName}.{ServiceName}.{MethodName} failed with error {ErrorCode} - {ErrorMessage}", appName, serviceName, methodName, response.Error.Code, response.Error.Description);
            }
            else
            {
                logger.LogInformation("Method call to {AppName}.{ServiceName}.{MethodName} failed was successful", appName, serviceName, methodName);
            }

            return response;
        }
        catch (System.Exception e)
        {
            logger.LogError("Method call to {AppName}.{ServiceName}.{MethodName} failed with error {ErrorMessage}", appName, serviceName, methodName, e.ToString());
            return new Error(ErrorCode.Exception, e.ToString());
        }
    }
}
