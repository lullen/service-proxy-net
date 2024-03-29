
//using System;
//using System.Threading.Tasks;
//using Dapr.Client;
//using Google.Protobuf;
//using Luizio.ServiceProxy.Models;

//namespace Luizio.ServiceProxy.Client
//{
//    public class DaprServiceProxy : IServiceProxy
//    {
//        public async Task<Response<TRes>> Invoke<T, TRes>(string app, string method, T request)
//            where T : class, new()
//            where TRes : class, new()
//        {
//            using var dapr = new DaprClientBuilder().Build();

//            try
//            {
//                var response = await dapr.InvokeMethodAsync<T, TRes>(app, method, request);
//                return new Response<TRes>(response);
//            }
//            catch (System.Exception e)
//            {
//                var methodSplit = method.LastIndexOf(".");
//                var clazz = method.Substring(0, methodSplit);
//                var methodName = method.Substring(methodSplit);

//                var type = Type.GetType($"{app}.Interfaces.{clazz}");
//                var methodInfo = type.GetMethod(methodName);

//                var error = new Error(ErrorCode.Exception, e.ToString());
//                var objectResponse = Activator.CreateInstance(methodInfo.ReturnType.GetGenericArguments()[0], new object[] { error });
//                return objectResponse as Response<TRes>;
//            }

//        }
//    }
//}
