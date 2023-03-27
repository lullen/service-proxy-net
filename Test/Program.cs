using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proxy.Client;
using Proxy.Server;
using Server;
using Server.Interfaces;

namespace Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            Console.WriteLine("Starting app");
            var t = host.Services.GetRequiredService<Testing>();
            while (true)
            {
                await t.Run();
            }

            Console.WriteLine("Completed app");
            Console.ReadLine();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services.AddProxyClient(ProxyType.InProc)
                            .AddService<ServiceImpl>()
                            //.AddTransient<ServiceOne, ServiceImpl>()
                            //.AddTransient<ServiceTwo, ServiceImpl>()
                            .AddTransient<Testing>()
                );

    }
}
