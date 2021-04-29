using System;
// using Microsoft.AspNetCore.Mvc.ModelBinding;
// using Microsoft.AspNetCore.Http;

namespace Proxy
{
    //public class GrpcServiceProxy : IServiceProxy
    //{
    //    private readonly IServiceProvider scope;
    //    private readonly CurrentUser _currentUser;
    //    private readonly IServiceInvoker _serviceInvoker;

    // //    public GrpcServiceProxy(IServiceProvider scope, CurrentUser currentUser, IServiceInvoker serviceInvoker)
    // //    {
    // //        this.scope = scope;
    // //        _currentUser = currentUser;
    // //        _serviceInvoker = serviceInvoker;
    // //    }

    // //    public T Create<T>() where T : class, IService
    // //    {
    // //        GrpcClientFactory.AllowUnencryptedHttp2 = true;
    // //        var clientFactory = scope.GetRequiredService<Grpc.Net.ClientFactory.GrpcClientFactory>();
    // //        var client = clientFactory.CreateClient<T>(typeof(T).FullName);

    // //        var logger = scope.GetRequiredService<ILogger<T>>();
    // //        var result = ServiceProxy<T>.Create(client, logger, _currentUser, _serviceInvoker);

    // //        return result;
    // //    }
    // //}

    // public class HttpServiceProxy : IServiceProxy
    // {
    //     private readonly IServiceProvider scope;
    //     private readonly CurrentUser _currentUser;


    //     public HttpServiceProxy(IServiceProvider scope, CurrentUser currentUser)
    //     {
    //         this.scope = scope;
    //         _currentUser = currentUser;
    //     }

    //     public T Create<T>() where T : class, IService
    //     {
    //         var logger = scope.GetRequiredService<ILogger<T>>();
    //         var serviceInvoker = scope.GetRequiredService<IServiceInvoker>();
    //         var result = ServiceProxy<T>.Create(null, logger, _currentUser, serviceInvoker);

    //         return result;
    //     }
    // }

}
