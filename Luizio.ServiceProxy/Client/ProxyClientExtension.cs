
using System;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Server;
using System.Linq;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Common;

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
		var interfaces = typeof(T).GetInterfaces()
			.Where(i => i != typeof(IService) && !i.IsGenericType && !i.IsNested && !i.IsSpecialName && !i.FullName.StartsWith("System."))
			.ToList();

		foreach (var iface in interfaces)
		{
			services.AddKeyedTransient(iface, typeof(T).Name.ToLower(), typeof(T));
		}
		services.AddKeyedTransient(typeof(T), typeof(T).Name.ToLower());

		return services;
	}
}
