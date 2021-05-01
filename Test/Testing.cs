using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Proxy;
using Proxy.NewProxy;
using Proxy.Server;
using Server.Interfaces;
namespace Test
{


    public class Testing
    {
        private readonly ServiceProxy sp;

        public Testing(ServiceProxy sp)
        {
            this.sp = sp;
        }
        public async Task Run()
        {
            ServiceLoader.RegisterServices(
                typeof(ServiceOne),
                typeof(ServiceTwo)
            );

            var ress = await sp.Create<ServiceOne>().MethodOne(new MethodRequestOne { Text = "Hi there!" })
                .Next((res) => sp.Create<ServiceTwo>().MethodTwo(new MethodRequestTwo { Text = res.Text + " Hello " }))
                .Next((res) => sp.Create<ServiceTwo>().MethodThree(new MethodRequestThree { Text = res.Text + " there!" }))
                .OnError((error) =>
                {
                    Console.WriteLine("ERROR!!! " + error.Description);
                    return Task.FromResult(new Response<MethodResponseThree>(error));
                });

            if (ress.HasError)
            {
                Console.WriteLine("Returned error " + ress.Error.Description);
            }
            else
            {
                Console.WriteLine("Returned " + ress.Result.Text);
            }

        }
    }
}