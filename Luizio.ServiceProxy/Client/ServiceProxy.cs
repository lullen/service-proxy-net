using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;

namespace Luizio.ServiceProxy.Client;

public class ServiceProxy<T> : DispatchProxy where T : class, IService
{
    private IServiceProvider? _sp;
    private string? _app;
    private string? _service;
    private ProxyType _proxyType;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        _ = targetMethod ?? throw new InvalidOperationException("Target method is null");
        _ = _sp ?? throw new InvalidOperationException("ServiceProvider is null");
        _ = _app ?? throw new InvalidOperationException("App is null");
        _ = _service ?? throw new InvalidOperationException("Service is null");
        _ = _proxyType == ProxyType.None ? throw new InvalidOperationException("ProxyType is null") : _proxyType;

        if (args == null || args.Length == 0 || args[0] == null)
        {
            throw new InvalidOperationException("Must have exactly one argument!");
        }

        if (!IsTaskOfResponse(targetMethod.ReturnType))
        {
            throw new InvalidOperationException("Return type must be of type Task<Response<T>>!");
        }

        var proxy = GetServiceProxy(_proxyType, _sp);

        var invoke = proxy.GetType().GetMethod("Invoke");
        var argumentType = args[0]!.GetType();
        var returnType = targetMethod!.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
        var method = invoke!.MakeGenericMethod(argumentType, returnType);

        return method.Invoke(proxy, new[] { _app, _service, targetMethod.Name, args[0] });
    }
    private IServiceProxy GetServiceProxy(ProxyType proxyType, IServiceProvider sp)
    {
        var currentUser = sp.GetRequiredService<CurrentUser>();
        return proxyType switch
        {
            ProxyType.None => throw new NotImplementedException(),
            ProxyType.HTTP => new HttpServiceProxy(sp, currentUser),
            ProxyType.InProc => new InProcServiceProxy(sp, currentUser),
            _ => throw new NotImplementedException()
        };
    }
    private bool IsTaskOfResponse(Type type)
    {
        if (type.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return false;
        }

        if (type.GetGenericArguments()[0].GetGenericTypeDefinition() != typeof(Response<>))
        {
            return false;
        }

        return true;
    }

    public static T Create(ProxyType proxyType, IServiceProvider sp, string app, string service)
    {
        object proxy = Create<T, ServiceProxy<T>>();
        var serviceProxy = (ServiceProxy<T>)proxy;
        serviceProxy._sp = sp;
        serviceProxy._app = app;
        serviceProxy._service = service;
        serviceProxy._proxyType = proxyType;
        return (T)proxy;
    }
}

public class ServiceProxy
{
    private IServiceProvider? _sp;
    private ProxyType _proxyType;

    public ServiceProxy(IServiceProvider sp, ProxyType proxyType)
    {
        _sp = sp;
        _proxyType = proxyType;
    }

    public T Create<T>(string app, string service) where T : class, IService
    {
        _ = _sp ?? throw new InvalidOperationException("ServiceProxy is not initalized.");
        object proxy = ServiceProxy<T>.Create(_proxyType, _sp, app, service);
        return (T)proxy;
    }
}

public static class StaticServiceProxy
{
    private static IServiceProvider? _sp;
    private static ProxyType _proxyType;

    public static void Init(IServiceProvider sp, ProxyType proxyType)
    {
        _sp = sp;
        _proxyType = proxyType;
    }

    public static T Create<T>(string app, string service) where T : class, IService
    {
        _ = _sp ?? throw new InvalidOperationException("ServiceProxy is not initalized.");
        object proxy = ServiceProxy<T>.Create(_proxyType, _sp, app, service);
        return (T)proxy;
    }
}