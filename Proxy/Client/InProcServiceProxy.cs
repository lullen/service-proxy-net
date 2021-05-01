
using System.Threading.Tasks;
using Google.Protobuf;
using Proxy.Models;
using Proxy.Server;

namespace Proxy.Client
{
    public class InProcServiceProxy : IServiceProxy
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
            using var scope = _serviceLoader.CreateScope();
            var service = _serviceLoader.Create(method, scope);
            var methodToInvoke = _serviceLoader.GetMethod(method, service);
            try
            {
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
