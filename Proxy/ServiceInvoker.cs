// using System;
// using System.Threading.Tasks;
// using System.Reflection;
// using System.Diagnostics;
// // using Microsoft.AspNetCore.Mvc.ModelBinding;
// // using Microsoft.AspNetCore.Http;

// namespace Proxy
// {
//     public class ServiceInvoker : IServiceInvoker
//     {
//         public async Task<T> Invoke<T>(object proxiedObject, MethodInfo targetMethod, object[] args, ILogger logger, CurrentUser currentUser) where T : class
//         {
//             var sw = new Stopwatch();
//             sw.Start();
//             var result = targetMethod.Invoke(proxiedObject, args);
//             if (result is Task<T> resultTask && IsTaskOfResponseWrapper(resultTask))
//             {
//                 try
//                 {
//                     var methodResult = await resultTask;
//                     LogResult(targetMethod, logger, currentUser.UserId, sw, methodResult, args);
//                     return methodResult;
//                 }
//                 catch (Exception e)
//                 {
//                     LogException(targetMethod, args, logger, currentUser.UserId, e);

//                     var error = new Error(ErrorCode.Exception, e.ToString());
//                     var objectResponse = Activator.CreateInstance(targetMethod.ReturnType.GetGenericArguments()[0], new object[] { error });
//                     if (objectResponse == null)
//                     {
//                         throw new Exception($"Couldn't create error for type {targetMethod.ReturnType.GetGenericArguments()[0]}");
//                     }
//                     return (T)objectResponse;
//                 }
//             }
//             else
//             {
//                 throw new NotSupportedException($"Only {nameof(Task<ResponseWrapper>)} is allowed as a response when using the {nameof(ServiceProxy)}");
//             }
//         }

//         private static void LogException(MethodInfo targetMethod, object[] args, ILogger logger, Guid userId, Exception e)
//         {
//             logger.LogError(e,
//                             "User {UserId} failed to call {TypeName}.{MethodName} with arguments: {Arguments}",
//                             userId,
//                             targetMethod.DeclaringType?.FullName,
//                             targetMethod.Name,
//                             JsonSerializer.Serialize(args));
//         }

//         private static void LogResult<T>(MethodInfo targetMethod, ILogger logger, Guid userId, Stopwatch sw, T methodResult, object[] args)
//         {
//             var error = GetError(methodResult);
//             if (error == null || !error.HasError)
//             {
//                 logger.LogInformation(
//                     "User {UserId} called {TypeName}.{MethodName} in {ElapsedMilliseconds}",
//                     userId,
//                     targetMethod.DeclaringType?.FullName,
//                     targetMethod.Name,
//                     sw.ElapsedMilliseconds);
//             }
//             else
//             {
//                 logger.LogWarning(
//                     "User {UserId} called {TypeName}.{MethodName} with code {Code} and description \"{Description}\" with arguments: {Arguments}",
//                     userId,
//                     targetMethod.DeclaringType?.FullName,
//                     targetMethod.Name,
//                     error.Code,
//                     error.Description,
//                     JsonSerializer.Serialize(args));
//             }
//         }

//         private static bool IsTaskOfResponseWrapper(object objectToCheck)
//         {
//             var type = objectToCheck.GetType().GetGenericArguments()[0];
//             var ofType = typeof(ResponseWrapper).IsAssignableFrom(type);
//             return ofType;
//         }

//         public static Error? GetError(object? objectToCheck)
//         {
//             if (objectToCheck == null)
//             {
//                 return null;
//             }
//             var type = objectToCheck.GetType();
//             var errorProperty = type.GetProperty(nameof(ResponseWrapper.Error));
//             if (errorProperty == null)
//             {
//                 return null;
//             }
//             var error = errorProperty.GetValue(objectToCheck);
//             return error != null ? (Error)error : null;
//         }
//     }

// }
