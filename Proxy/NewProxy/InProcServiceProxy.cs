
using System;
using System.Threading.Tasks;
using Dapr.Client;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;

namespace Proxy.NewProxy
{
    public class InProcServiceProxy : Proxy.NewProxy.IServiceProxy
    {
        private readonly IServiceScopeFactory _sp;

        public InProcServiceProxy(IServiceScopeFactory sp)
        {
            _sp = sp;
        }

        public Task<Response<TRes>> Invoke<T, TRes>(string app, string method, T request)
            where T : class, IMessage, new()
            where TRes : class, IMessage, new()
        {
            using (var scope = _sp.CreateScope())
            {
                var methodSplit = method.LastIndexOf(".");
                var clazz = method.Substring(0, methodSplit);
                var methodName = method.Substring(methodSplit + 1);

                // request.GetType().Assembly.GetType()

                var typeName = $"{app}.Interfaces.{clazz}";
                var type = request.GetType().Assembly.GetType(typeName);
                var methodInfo = type.GetMethod(methodName);
                var service = scope.ServiceProvider.GetRequiredService(type);
                var res = methodInfo.Invoke(service, new[] { request });
                var task = res as Task<Response<TRes>>;
                return task;
            }
        }
    }
}
