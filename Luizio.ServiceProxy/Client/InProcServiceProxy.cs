
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Models;
using Proxy.Server;

namespace Luizio.ServiceProxy.Client;

public class InProcServiceProxy : IServiceProxy
{
    private readonly IServiceProvider sp;
    private readonly CurrentUser currentUser;

    public InProcServiceProxy(IServiceProvider sp, CurrentUser currentUser)
    {
        this.sp = sp;
        this.currentUser = currentUser;
    }

    public async Task<Response<TRes>> Invoke<T, TRes>(string appName, string serviceName, string methodName, T request)
        where T : class, new()
        where TRes : class, new()
    {
        using var scope = sp.CreateScope();
        var newCurrentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
        newCurrentUser.Metadata = currentUser.Metadata;
        newCurrentUser.Token = currentUser.Token;

        var serviceImpl = ServiceStore.GetService(serviceName, scope.ServiceProvider);
        var methodToInvoke = ServiceStore.GetMethod(methodName, serviceImpl);
        try
        {
            var res = methodToInvoke.Invoke(serviceImpl, new[] { request });
            var task = res as Task<Response<TRes>>;
            return await task!;
        }
        catch (System.Exception e)
        {
            return new Response<TRes>(new Error(ErrorCode.Exception, e.ToString()));
        }
    }
}
