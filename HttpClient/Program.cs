using Microsoft.Extensions.Primitives;
using Proxy.Client;
using Server.Interfaces;
using System.IO;
using System.Runtime.CompilerServices;

var builder = WebApplication.CreateBuilder(args);


var proxyType = builder.Configuration.GetValue<ProxyType>("ProxyType");
// Add services to the container.
builder.Services.AddMvc();
builder.Services.AddProxyClient(proxyType);
builder.Services.Configure<ServiceSettings>(builder.Configuration);
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseClientProxy(proxyType);
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapGet("/", async (ServiceProxy sp, CurrentUser user) =>
{
    //var streamFile = System.IO.File.OpenRead("test.txt");
    //var resFile = await StaticServiceProxy.Create<ServiceOne>("HttpServer", "ServiceImpl").UploadFile(new FileTestRequest
    //{
    //    Id = Guid.NewGuid(),
    //    Name = "test" + DateTime.Now.Second,
    //    File = streamFile
    //});
    //return;
    while (true)
    {
        user.Metadata = new Dictionary<string, StringValues>
        {
            { "test", "test" },
            { "Authorization", "Bearer 123" }
        };
        var res1 = await sp.Create<ServiceOne>("HttpServer", "ServiceImpl").MethodOne(new MethodRequestOne { Text = "Hi from Method one!" });
        var res2 = await sp.Create<ServiceTwo>("HttpServer", "ServiceImpl").MethodTwo(new MethodRequestTwo { Text = res1.Result.Text + " Hi from Method two!" });
        var res3 = await sp.Create<ServiceTwo>("HttpServer", "ServiceImpl").MethodThree(new MethodRequestThree { Text = res2.Result.Text + " Hi from Method three!" });
        var stream = System.IO.File.OpenRead("test.txt");
        var res4 = await sp.Create<ServiceOne>("HttpServer", "ServiceImpl").UploadFile(new FileTestRequest
        {
            Id = Guid.NewGuid(),
            Name = "test" + DateTime.Now.Second,
            File = stream
        });
        Thread.Sleep(5000);
    }
    //var ress = res3;
    ////var ress = await ServiceProxy.Create<ServiceOne>("HttpServer", "ServiceImpl").MethodOne(new MethodRequestOne { Text = "Hi from Method one!" })
    ////            .Next((res) => ServiceProxy.Create<ServiceTwo>("HttpServer", "ServiceImpl").MethodTwo(new MethodRequestTwo { Text = res.Text + " Hi from Method two!" }))
    ////            .Next((res) => ServiceProxy.Create<ServiceTwo>("HttpServer", "ServiceImpl").MethodThree(new MethodRequestThree { Text = res.Text + " Hi from Method three!" }))
    ////            .OnError((error) =>
    ////            {
    ////                Console.WriteLine("ERROR!!! " + error.Description);
    ////                return Task.FromResult(new Response<MethodResponseThree>(error));
    ////            });

    //if (ress.HasError)
    //{
    //    return ress.Error.Description;
    //}
    //else
    //{
    //    return ress.Result.Text;
    //}
});
app.Run();
