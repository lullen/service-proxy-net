
using System;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Server;
using System.Linq;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public static class ProxyClientExtensions
{
    public static IServiceCollection AddProxyClient(this IServiceCollection services, ProxyType proxyType)
    {
        if (proxyType == ProxyType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(proxyType));
        }
        services.AddScoped<CurrentUser>();
        services.AddTransient<IProxy, Proxy>(f => new Proxy(f.GetRequiredService<IServiceProvider>(), proxyType));

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

    //public static IServiceCollection AddInProcProxyClient(this IServiceCollection services, params Type[] hostedServices)
    //{
    //    ServiceStore.RegisterServices(hostedServices);
    //    return services;
    //}
    public static IServiceCollection AddService<T>(this IServiceCollection services) where T : class
    {
        ServiceStore.RegisterService(typeof(T));
        if (!services.Any(s => s.ServiceType == typeof(T)))
        {
            services.AddTransient(typeof(T));
        }
        return services;
    }
}
