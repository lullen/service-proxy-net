
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Common;
using Luizio.ServiceProxy.Messaging;

namespace Luizio.ServiceProxy.Server;

public static class ProxyServerExtensions
{
    public static IServiceCollection AddProxyServer(this IServiceCollection services)
    {
        services.AddScoped<CurrentUser>();
        return services;
    }
}
