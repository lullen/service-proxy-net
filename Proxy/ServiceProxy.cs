// using System;
// using Microsoft.Extensions.DependencyInjection;
// using System.Reflection;
// using System.Text.Json;
// using Microsoft.Extensions.Logging;
// using System.Diagnostics;
// using System.Net.Http;
// // using Microsoft.AspNetCore.Mvc.ModelBinding;
// // using Microsoft.AspNetCore.Http;
// using System.Globalization;
// using System.ComponentModel;
// using System.Text.Json.Serialization;
// using Microsoft.Extensions.Options;

// namespace Proxy
// {
//     public class ServiceProxy<T> : DispatchProxy where T : class, IService
//     {
//         private T? proxiedObject;
//         private ILogger<T>? logger;
//         private CurrentUser? currentUser;
//         private IServiceInvoker? serviceInvoker;

//         protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
//         {
//             var sw = new Stopwatch();
//             sw.Start();

//             try
//             {
//                 if (serviceInvoker == null)
//                 {
//                     throw new ArgumentNullException(nameof(serviceInvoker));
//                 }
//                 if (targetMethod == null)
//                 {
//                     throw new ArgumentNullException(nameof(targetMethod));
//                 }
//                 if (currentUser == null)
//                 {
//                     throw new ArgumentNullException(nameof(currentUser));
//                 }

//                 var method = typeof(IServiceInvoker).GetMethod("Invoke");
//                 if (method == null)
//                 {
//                     throw new ArgumentException("Could not create method \"Invoke\" in service proxy");
//                 }
//                 var generic = method.MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]);
//                 if (method == null || generic == null)
//                 {
//                     throw new ArgumentException("Could not create \"MakeGenericMethod\" in service proxy");
//                 }
//                 return generic.Invoke(serviceInvoker, new object?[] { proxiedObject, targetMethod, args, logger, currentUser });
//             }
//             catch (System.Exception e)
//             {
//                 logger.LogError(e, "Could not call function {TypeName}.{MethodName}",
//                         targetMethod?.DeclaringType?.FullName,
//                         targetMethod?.Name);
//                 throw;
//             }
//             //  // To simulate going over the wire we serialize the result.
//             //  // This means that we can never manipulate the object in another layer.

//             ////  return Clone(result, type);

//             //  var copiedResult = JsonSerializer.Deserialize(JsonSerializer.Serialize(result), targetMethod.ReturnType);
//             //  return copiedResult;
//         }

//         public static T Create(T? decorated, ILogger<T> logger, CurrentUser currentUser, IServiceInvoker serviceInvoker)
//         {
//             object proxy = Create<T, ServiceProxy<T>>();

//             var serviceProxy = (ServiceProxy<T>)proxy;
//             serviceProxy.proxiedObject = decorated;
//             serviceProxy.logger = logger;
//             serviceProxy.serviceInvoker = serviceInvoker;
//             serviceProxy.currentUser = currentUser;

//             return (T)proxy;
//         }

//     }

//     public class ServiceProxy : IServiceProxy
//     {
//         private readonly IServiceScopeFactory _sp;
//         private readonly CurrentUser _currentUser;
//         private readonly IServiceInvoker _serviceInvoker;

//         public ServiceProxy(IServiceScopeFactory sp, CurrentUser currentUser, IServiceInvoker serviceInvoker)
//         {
//             _sp = sp;
//             _currentUser = currentUser;
//             _serviceInvoker = serviceInvoker;
//         }

//         public T Create<T>() where T : class, IService
//         {
//             var scope = _sp.CreateScope();
//             var newCurrentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
//             newCurrentUser.Init(_currentUser.UserId, _currentUser.Roles, _currentUser.MemberRights, _currentUser.Permissions, _currentUser.Token);

//             var logger = scope.ServiceProvider.GetRequiredService<ILogger<T>>();
//             var service = scope.ServiceProvider.GetRequiredService<T>();
//             var result = ServiceProxy<T>.Create(service, logger, newCurrentUser, _serviceInvoker);
//             return result;
//         }
//     }

// }
