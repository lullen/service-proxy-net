using System;
using System.Linq;
using System.Threading.Tasks;
using Proxy.Client;
using Proxy.Models;
using Proxy.Server;
using Server.Interfaces;
namespace Test;



public class Testing
{
    private readonly ServiceProxy sp;
    private readonly CurrentUser currentUser;

    public Testing(ServiceProxy sp, CurrentUser currentUser)
    {
        this.sp = sp;
        this.currentUser = currentUser;
    }
    public async Task Run()
    {
        if (!currentUser.Metadata.Any())
        {
            currentUser.Metadata.Add("Test", "Testing");
            currentUser.Metadata.Add("Authorization", "Bearer sdgsdf3245236");
        }
        var ress = await sp.Create<ServiceOne>("Server", "ServiceImpl").MethodOne(new MethodRequestOne { Text = "Hi there!" })
            .Next((res) => sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodTwo(new MethodRequestTwo { Text = res.Text + " Hello " }))
            .Next((res) => sp.Create<ServiceTwo>("Server", "ServiceImpl").MethodThree(new MethodRequestThree { Text = res.Text + " there!" }))
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