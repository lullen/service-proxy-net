
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Server;

namespace Proxy.Client;

public static class ProxyClientExtensions
{
    public static IServiceCollection AddProxyClient(this IServiceCollection services, ProxyType proxyType)
    {
        if (proxyType == ProxyType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(proxyType));
        }
        services.AddScoped<CurrentUser>();
        services.AddTransient(f => new ServiceProxy(f.GetRequiredService<IServiceProvider>(), proxyType));

        //if (proxyType == ProxyType.HTTP)
        //{
        //    services.AddTransient<IServiceProxy, HttpServiceProxy>();
        //}
        //else if (proxyType == ProxyType.InProc)
        //{
        //    services.AddTransient<IServiceProxy, InProcServiceProxy>();
        //}
        //else
        //{
        //    throw new ArgumentOutOfRangeException(nameof(proxyType));
        //}

        return services;
    }

    public static IApplicationBuilder UseClientProxy(this IApplicationBuilder builder, ProxyType proxyType)
    {
        StaticServiceProxy.Init(builder.ApplicationServices, proxyType);
        return builder;
    }

    //public static IServiceCollection AddInProcProxyClient(this IServiceCollection services, params Type[] hostedServices)
    //{
    //    ServiceStore.RegisterServices(hostedServices);
    //    return services;
    //}
    public static IServiceCollection AddService<T>(this IServiceCollection services) where T : class
    {
        ServiceStore.RegisterService(typeof(T));
        services.AddTransient(typeof(T));
        return services;
    }
}
