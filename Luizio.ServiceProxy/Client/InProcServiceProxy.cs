
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace Luizio.ServiceProxy.Client;

public class InProcServiceProxy<TClass>(IServiceProvider sp, CurrentUser currentUser) : IServiceProxy where TClass : class, IService
{
    public async Task<Response<TRes>> Invoke<TParam, TRes>(string appName, string serviceName, string methodName, TParam request)
        where TParam : class, new()
        where TRes : class
    {
        using var scope = sp.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InProcServiceProxy<TClass>>>();
        logger.LogInformation("Calling \"{AppName}.{ServiceName}.{MethodName}\".", appName, serviceName, methodName);

        var newCurrentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
        newCurrentUser.Metadata = currentUser.Metadata;
        newCurrentUser.Token = currentUser.Token;

        IService serviceImpl;
        MethodInfo methodToInvoke;
        try
        {
            serviceImpl = scope.ServiceProvider.GetRequiredKeyedService<TClass>(serviceName.ToLower());
            methodToInvoke = GetMethod(methodName, typeof(TParam), serviceImpl);
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

            var res = methodToInvoke.Invoke(serviceImpl, [request]);
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

    private void LogResponse<TRes>(string appName, string serviceName, string methodName, ILogger<InProcServiceProxy<TClass>> logger, Activity? proxyActivity, Response<TRes> response) where TRes : class
    {
        if (response.HasError)
        {
            logger.Log(GetLogLevel(response.Error.Code), "Method call to \"{AppName}.{ServiceName}.{MethodName}\" failed with error {ErrorCode} - \"{ErrorMessage}\".", appName, serviceName, methodName, response.Error.Code, response.Error.Description);
            proxyActivity?.SetStatus(ActivityStatusCode.Error);
        }
        else
        {
            logger.LogInformation("Method call to \"{AppName}.{ServiceName}.{MethodName}\" succeeded.", appName, serviceName, methodName);
            proxyActivity?.SetStatus(ActivityStatusCode.Ok);
        }
    }

    private static LogLevel GetLogLevel(ErrorCode code) => code switch
    {
        ErrorCode.AlreadyExists => LogLevel.Warning,
        ErrorCode.Error => LogLevel.Error,
        ErrorCode.Exception => LogLevel.Error,
        ErrorCode.InvalidInput => LogLevel.Warning,
        ErrorCode.NotFound => LogLevel.Warning,
        ErrorCode.Skipped => LogLevel.Information,
        ErrorCode.Unauthorized => LogLevel.Warning,
        _ => LogLevel.Information,
    };

    private static MethodInfo GetMethod(string methodName, Type parameterType, IService invokeClass)
    {
        MethodInfo? invokeMethod = null;
        methodName = methodName.ToLower();

        List<Type> types = [parameterType];
        if (parameterType.BaseType != null)
        {
            types.Add(parameterType.BaseType);
        }
        foreach (var method in invokeClass.GetType().GetMethods())
        {
            if (method.Name.ToLower() == methodName && types.Any(t => t == method.GetParameters().FirstOrDefault()?.ParameterType))
            {
                invokeMethod = method;
                break;
            }
        }

        if (invokeMethod == null)
        {
            throw new Exception($"Method \"{invokeClass.GetType().Name}.{methodName}\" with parameter \"{parameterType}\" not found.");
        }
        return invokeMethod;
    }
}
