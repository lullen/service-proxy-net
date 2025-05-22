
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

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
        where TRes : class
    {
        using var scope = sp.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InProcServiceProxy>>();
        logger.LogInformation("Calling \"{AppName}.{ServiceName}.{MethodName}\".", appName, serviceName, methodName);

        var newCurrentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
        newCurrentUser.Metadata = currentUser.Metadata;
        newCurrentUser.Token = currentUser.Token;

        IService serviceImpl;
        MethodInfo methodToInvoke;
        try
        {
            var serviceStore = scope.ServiceProvider.GetRequiredService<ServiceStore>();
            serviceImpl = serviceStore.GetService(serviceName, scope.ServiceProvider);
            methodToInvoke = serviceStore.GetMethod(methodName, typeof(T), serviceImpl);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not resolve service and method to invoke. Service name {ServiceName} with method name {MethodName}", serviceName, methodName);
            throw;
        }

        Activity? previousActivity = Activity.Current;
        Activity? proxyActivity = null;
        try
        {
            if (previousActivity is not null)
            {
                proxyActivity = ProxyActivitySource.Source.StartActivity($"{serviceName}.{methodName}", ActivityKind.Internal, previousActivity.Context);
                Activity.Current = proxyActivity;
            }

            var res = methodToInvoke.Invoke(serviceImpl, new[] { request });
            var task = res as Task<Response<TRes>>;
            var response = await task!;

            Activity.Current = previousActivity;

            LogResponse(appName, serviceName, methodName, logger, proxyActivity, response);

            return response;
        }
        catch (System.Exception e)
        {
            logger.LogError("Method call to {AppName}.{ServiceName}.{MethodName} failed with error {ErrorCode} - {ErrorMessage}.", appName, serviceName, methodName, ErrorCode.Exception, e.ToString());
            proxyActivity?.SetStatus(ActivityStatusCode.Ok);
            return new Error(ErrorCode.Exception, e.ToString());
        }
        finally
        {
            proxyActivity?.Stop();
            Activity.Current = previousActivity;
        }
    }

    private void LogResponse<TRes>(string appName, string serviceName, string methodName, ILogger<InProcServiceProxy> logger, Activity? proxyActivity, Response<TRes> response) where TRes : class
    {
        if (response.HasError)
        {
            logger.LogError("Method call to \"{AppName}.{ServiceName}.{MethodName}\" failed with error {ErrorCode} - \"{ErrorMessage}\".", appName, serviceName, methodName, response.Error.Code, response.Error.Description);
            proxyActivity?.SetStatus(ActivityStatusCode.Error);
        }
        else
        {
            logger.LogInformation("Method call to \"{AppName}.{ServiceName}.{MethodName}\" succeeded.", appName, serviceName, methodName);
            proxyActivity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}
