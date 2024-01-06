
using System;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Server;
using System.Linq;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public static class ProxyClientExtensions
{
	public static IServiceCollection AddProxyClient(this IServiceCollection services, ProxyType proxyType)
	{
		if (proxyType == ProxyType.None)
		{
			throw new ArgumentOutOfRangeException(nameof(proxyType));
		}
		services.AddScoped<CurrentUser>();
		services.AddTransient<IProxy, Proxy>(f => new Proxy(f.GetRequiredService<IServiceProvider>(), proxyType));
		services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(ProxyActivitySource.SourceName));
		return services;
	}

	public static IServiceCollection AddService<T>(this IServiceCollection services) where T : class
	{
		ServiceStore.RegisterService(typeof(T));
		if (!services.Any(s => s.ServiceType == typeof(T)))
		{
			services.AddTransient(typeof(T));
		}
		
		return services;
	}
}
