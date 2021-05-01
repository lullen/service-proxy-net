using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Type = System.Type;

namespace Proxy.Server
{
    public class DaprServer : AppCallback.AppCallbackBase
    {
        private readonly ServiceLoader _serviceLoader;

        public DaprServer(IServiceScopeFactory scopeFactory, ServiceLoader serviceLoader)
        {
            _serviceLoader = serviceLoader;
        }

        public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
        {
            var response = new InvokeResponse();

            using var scope = _serviceLoader.CreateScope();
            var service = _serviceLoader.Create(request.Method, scope);
            var method = _serviceLoader.GetMethod(request.Method, service);

            try
            {
                var requestData = GetRequestData(request.Data, method.GetParameters()[0].ParameterType);

                var task = (Task)method.Invoke(service, new[] { requestData })!;
                await task!.ConfigureAwait(false);

                var resultProperty = task.GetType().GetProperty("Result");
                response.Data = Any.Pack((IMessage)resultProperty!.GetValue(task)!);
            }
            catch (System.Exception e)
            {

                throw;
            }
            return response;
        }

        private object GetRequestData(Google.Protobuf.WellKnownTypes.Any data, Type requestType)
        {
            var method = data.GetType().GetMethod("Unpack")!.MakeGenericMethod(requestType);
            return (object)method.Invoke(data, null)!;
        }
    }
}