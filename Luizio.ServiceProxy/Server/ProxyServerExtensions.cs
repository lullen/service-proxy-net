
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Client;

namespace Luizio.ServiceProxy.Server;

public static class ProxyServerExtensions
{
    //public static IServiceCollection AddDaprServer(this IServiceCollection services)
    //{
    //    var hostedService = services.Where(s => s.ServiceType.IsAssignableFrom(typeof(IService))).Select(s => s.ServiceType).ToArray();
    //    ServiceStore.RegisterServices(hostedService);
    //    return services
    //        .AddTransient<DaprServer>();
    //}

    public static IServiceCollection AddProxyServer(this IServiceCollection services)
    {
        return services.AddScoped<CurrentUser>();
    }

}
