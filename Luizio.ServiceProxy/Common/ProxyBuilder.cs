
using System;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.ServiceProxy.Common;

public class ProxyBuilder
{
    internal readonly IServiceCollection Services;
    internal readonly ServiceStore ServiceStore;

    internal ProxyBuilder(IServiceCollection services, ServiceStore serviceStore)
    {
        Services = services;
        ServiceStore = serviceStore;
    }
}