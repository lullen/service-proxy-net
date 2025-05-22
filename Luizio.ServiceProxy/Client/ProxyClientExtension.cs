
using System;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Server;
using System.Linq;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Common;

namespace Luizio.ServiceProxy.Client;

public static class ProxyClientExtensions
{
	public static ProxyBuilder AddProxyClient(this IServiceCollection services, ProxyType proxyType)
	{
		if (proxyType == ProxyType.None)
		{
			throw new ArgumentOutOfRangeException(nameof(proxyType));
		}
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
		services.AddTransient<IProxy, Proxy>(f => new Proxy(f.GetRequiredService<IServiceProvider>(), proxyType));
		services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(ProxyActivitySource.SourceName));
		return proxyBuilder;
	}

	public static ProxyBuilder AddService<T>(this ProxyBuilder services) where T : class
	{
		services.ServiceStore.RegisterService(typeof(T));
		if (!services.Services.Any(s => s.ServiceType == typeof(T)))
		{
			services.Services.AddTransient(typeof(T));
		}

		return services;
	}
}
