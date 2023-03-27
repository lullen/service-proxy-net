
using System;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Client;

namespace Proxy.Server
{
    public static class ProxyServerExtensions
    {
        public static IServiceCollection AddDaprServer(this IServiceCollection services, params Type[] hostedServices)
        {
            ServiceStore.RegisterServices(hostedServices);
            return services
                .AddTransient<DaprServer>();
        }

        public static IServiceCollection AddProxyServer(this IServiceCollection services)
        {
            return services.AddScoped<CurrentUser>();
        }

    }
}
