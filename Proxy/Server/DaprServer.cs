//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Microsoft.Extensions.DependencyInjection;
//using Type = System.Type;

//namespace Proxy.Server
//{
//    public class DaprServer : AppCallback.AppCallbackBase
//    {
//        private readonly IServiceScopeFactory scopeFactory;
//        private readonly ServiceStore _serviceLoader;

//        public DaprServer(IServiceScopeFactory scopeFactory, ServiceStore serviceLoader)
//        {
//            this.scopeFactory = scopeFactory;
//            _serviceLoader = serviceLoader;
//        }

//        public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
//        {
//            var response = new InvokeResponse();

//            using var scope = scopeFactory.CreateScope();
//            var service = ServiceStore.GetService(request.Method, scope.ServiceProvider);
//            var method = ServiceStore.GetMethod(request.Method, service);

//            try
//            {
//                var requestData = GetRequestData(request.Data, method.GetParameters()[0].ParameterType);

//                var task = (Task)method.Invoke(service, new[] { requestData })!;
//                await task!.ConfigureAwait(false);

//                var resultProperty = task.GetType().GetProperty("Result");
//                response.Data = Any.Pack((IMessage)resultProperty!.GetValue(task)!);
//            }
//            catch (System.Exception e)
//            {

//                throw;
//            }
//            return response;
//        }

//        private object GetRequestData(Google.Protobuf.WellKnownTypes.Any data, Type requestType)
//        {
//            var method = data.GetType().GetMethod("Unpack")!.MakeGenericMethod(requestType);
//            return (object)method.Invoke(data, null)!;
//        }
//    }
//}