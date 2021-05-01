
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proxy.Server
{
    public static class ProxyServerExtensions
    {
        public static IServiceCollection AddDaprServer(this IServiceCollection services, params Type[] hostedServices)
        {
            ServiceLoader.RegisterServices(hostedServices);
            return services
                .AddTransient<ServiceLoader>()
                .AddTransient<DaprServer>();
        }
    }
}
