
using System;
using System.Threading.Tasks;
using Dapr.Client;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Server;

namespace Proxy.NewProxy
{
    public class InProcServiceProxy : Proxy.NewProxy.IServiceProxy
    {
        private readonly ServiceLoader _serviceLoader;

        public InProcServiceProxy(ServiceLoader serviceLoader)
        {
            _serviceLoader = serviceLoader;
        }

        public Task<Response<TRes>> Invoke<T, TRes>(string app, string method, T request)
            where T : class, IMessage, new()
            where TRes : class, IMessage, new()
        {
            

            // try
            // {
            //     var requestData = GetRequestData(request.Data, method.GetParameters()[0].ParameterType);

            //     var task = (Task)method.Invoke(invokeClass, new[] { requestData })!;
            //     await task!.ConfigureAwait(false);

            //     var resultProperty = task.GetType().GetProperty("Result");
            //     response.Data = Any.Pack((IMessage)resultProperty!.GetValue(task)!);
            //     return response;
            // }
            // catch (System.Exception e)
            // {

            //     throw;
            // }
            
            using var scope = _serviceLoader.CreateScope();
            var service = _serviceLoader.Create(method, scope);
            var methodToInvoke = _serviceLoader.GetMethod(method, service);
            try
            {
                // var methodSplit = method.LastIndexOf(".");
                // var clazz = method.Substring(0, methodSplit);
                // var methodName = method.Substring(methodSplit + 1);

                // var typeName = $"{app}.Interfaces.{clazz}";
                // var type = request.GetType().Assembly.GetType(typeName);
                // var methodInfo = type!.GetMethod(methodName);
                // var service = scope.ServiceProvider.GetRequiredService(type);
                var res = methodToInvoke!.Invoke(service, new[] { request });
                var task = res as Task<Response<TRes>>;
                return task!;
            }
            catch (System.Exception e)
            {
                return Task.FromResult(new Response<TRes>(new Error(ErrorCode.Exception, e.ToString())));
            }
        }
    }
}
