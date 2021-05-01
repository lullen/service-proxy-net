using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proxy.NewProxy;
using Proxy.Server;
using Server;
using Server.Interfaces;

namespace Test
{
    class Program
    {
        static Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();
            Console.WriteLine("Starting app");
            var t = host.Services.GetRequiredService<Testing>();
            t.Run();

            Console.WriteLine("Completed app");
            return host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services.AddTransient<IServiceProxy, InProcServiceProxy>()
                            .AddScoped<ServiceProxy>()
                            .AddTransient<ServiceLoader>()
                            .AddTransient<ServiceOne, ImplService>()
                            .AddTransient<ServiceTwo, ImplService>()
                            .AddTransient<Testing>()
                );

    }
}
