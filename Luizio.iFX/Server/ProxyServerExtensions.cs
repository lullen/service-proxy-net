
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Luizio.iFX.Models;
using Luizio.iFX.Common;
using Luizio.iFX.Messaging;

namespace Luizio.iFX.Server;

public static class ProxyServerExtensions
{
    public static IServiceCollection AddProxyServer(this IServiceCollection services)
    {
        services.AddScoped<CurrentUser>();
        return services;
    }
}
