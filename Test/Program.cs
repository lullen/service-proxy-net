using System;
using Microsoft.Extensions.DependencyInjection;
using Luizio.ServiceProxy.Client;
using Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Test;

class Program
{
	static void Main(string[] args)
	{
		var app = CreateHostBuilder(args).Build();

		app.MapGet("/", async (IServiceProvider sp) =>
		{
			var testing = sp.GetRequiredService<Testing>();
			return await testing.Run();
		});
		
		app.Run();
	}

	static WebApplicationBuilder CreateHostBuilder(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.Services
			.AddTransient<Testing>()
			.AddProxyClient(ProxyType.InProc)
			.AddService<ServiceImpl>();
			//.AddTransient<ServiceOne, ServiceImpl>()
			//.AddTransient<ServiceTwo, ServiceImpl>()

		return builder;
	}

}
