
using System;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Server;

namespace Proxy.Client
{
    public static class ProxyClientExtensions
    {
        public static IServiceCollection AddDaprProxyClient(this IServiceCollection services)
        {
            return services
                .AddTransient<IServiceProxy, DaprServiceProxy>()
                .AddTransient<ServiceProxy>();
        }

        public static IServiceCollection AddInProcProxyClient(this IServiceCollection services, params Type[] hostedServices)
        {
            ServiceLoader.RegisterServices(hostedServices);
            return services
                .AddTransient<IServiceProxy, InProcServiceProxy>()
                .AddTransient<ServiceLoader>()
                .AddTransient<ServiceProxy>();
        }
    }
}
