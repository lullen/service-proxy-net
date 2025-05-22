
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Common;

namespace Luizio.ServiceProxy.Server;

public static class ProxyServerExtensions
{
    //public static IServiceCollection AddDaprServer(this IServiceCollection services)
    //{
    //    var hostedService = services.Where(s => s.InterfaceType.IsAssignableFrom(typeof(IService))).Select(s => s.InterfaceType).ToArray();
    //    ServiceStore.RegisterServices(hostedService);
    //    return services
    //        .AddTransient<DaprServer>();
    //}

    public static ProxyBuilder AddProxyServer(this IServiceCollection services)
    {
        ProxyBuilder proxyBuilder;
        if (!services.Any(s => s.ServiceType == typeof(ServiceStore)))
        {
            var serviceStore = new ServiceStore();
            services.AddSingleton(serviceStore);
            proxyBuilder = new ProxyBuilder(services, serviceStore);
        }
        else
        {
            throw new InvalidOperationException("ServiceStore already registered.");
        }
        services.AddScoped<CurrentUser>();
        return proxyBuilder;
    }

}
