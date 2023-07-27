using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Server;
using Server;
using Server.Interfaces;

namespace Test;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = CreateHostBuilder(args).Build();
        host.UseClientProxy(ProxyType.InProc);

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
