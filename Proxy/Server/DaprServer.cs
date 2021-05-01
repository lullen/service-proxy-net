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
                return response;
            }
            catch (System.Exception e)
            {

                throw;
            }
            return response;
            // switch (request.Method)
            // {

            //     case "getaccount":
            //         var input = request.Data.Unpack<GrpcServiceSample.Generated.GetAccountRequest>();
            //         var output = await GetAccount(input, context);
            //         response.Data = Any.Pack(output);
            //         break;
            //     case "deposit":
            //     case "withdraw":
            //         var transaction = request.Data.Unpack<GrpcServiceSample.Generated.Transaction>();
            //         var account = request.Method == "deposit" ?
            //             await Deposit(transaction, context) :
            //             await Withdraw(transaction, context);
            //         response.Data = Any.Pack(account);
            //         break;
            //     default:
            //         break;
            // }
        }

        private object GetRequestData(Google.Protobuf.WellKnownTypes.Any data, Type requestType)
        {
            var method = data.GetType().GetMethod("Unpack")!.MakeGenericMethod(requestType);
            return (object)method.Invoke(data, null)!;
        }
    }
}