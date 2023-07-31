
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;

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

    public static IServiceCollection AddProxyServer(this IServiceCollection services)
    {
        return services.AddScoped<CurrentUser>();
    }

}
