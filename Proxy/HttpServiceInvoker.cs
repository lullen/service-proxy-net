// using System;
// using System.Threading.Tasks;
// using System.Reflection;
// using System.Diagnostics;
// using System.Text;
// using System.Linq;
// using System.IO;
// using System.Net;
// // using Microsoft.AspNetCore.Mvc.ModelBinding;
// // using Microsoft.AspNetCore.Http;

// namespace Proxy
// {
//     public class HttpServiceInvoker : IServiceInvoker
//     {
//         private readonly HttpClient _httpClient;
//         private const string HeaderPrefix = "Mv-Result";
//         private readonly ProxySettings _settings;

//         public HttpServiceInvoker(HttpClient httpClient, IOptions<ProxySettings> options)
//         {
//             this._httpClient = httpClient;
//             _settings = options.Value;
//         }

//         public async Task<T> Invoke<T>(object proxiedObject, MethodInfo targetMethod, object[] args, ILogger logger, CurrentUser currentUser) where T : class
//         {
//             var sw = new Stopwatch();
//             sw.Start();
//             Uri uri = CreateUri(targetMethod);
//             var resultString = string.Empty;
//             var resultCode = HttpStatusCode.OK;
//             try
//             {
//                 var jsonOptions = new JsonSerializerOptions
//                 {
//                     PropertyNameCaseInsensitive = true,
//                     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                 };
//                 var model = args[0];
//                 HttpResponseMessage? result;
//                 if (ContainsStream(model.GetType()))
//                 {
//                     result = await UploadFile(uri, model, currentUser);
//                 }
//                 else
//                 {
//                     result = await PostData(uri, model, currentUser, jsonOptions);
//                 }

//                 if (ContainsStream(typeof(T)))
//                 {
//                     return await DownloadFile<T>(result);
//                 }
//                 else
//                 {
//                     resultCode = result.StatusCode;
//                     resultString = await result.Content.ReadAsStringAsync();
//                     var methodResult = JsonSerializer.Deserialize(resultString, typeof(T), jsonOptions);
//                     if (methodResult == null)
//                     {
//                         return (T)CreateErrorModel(targetMethod.ReturnType, "Could not parse resultString");
//                     }
//                     if (IsResponseWrapper(methodResult))
//                     {
//                         LogResult(targetMethod, logger, currentUser.UserId, sw, methodResult, args);
//                         return (T)methodResult;
//                     }
//                     else
//                     {
//                         throw new NotSupportedException($"Only {nameof(Task<ResponseWrapper>)} is allowed as a response when using the {nameof(ServiceProxy)}");
//                     }

//                 }
//             }
//             catch (Exception e)
//             {
//                 LogException(targetMethod, args, logger, currentUser.UserId, e);
//                 return (T)CreateErrorModel(targetMethod.ReturnType, $"Http status code was '${resultCode}' and response was '{resultString}'." +
//                     $" {uri.AbsoluteUri}" +
//                     $" {e}");
//             }
//         }

//         private Uri CreateUri(MethodInfo targetMethod)
//         {

//             var appName = _settings.VeraApplicationName;
//             var serviceName = targetMethod.DeclaringType?.Namespace?.Replace(".Interfaces", string.Empty);
//             var controller = serviceName + "." + targetMethod.DeclaringType?.Name ?? throw new ArgumentNullException("Controller name not found");

//             var endpoint = targetMethod.Name;

//             if (RuntimeEnvironment.IsServiceFabric)
//             {
//                 var uri = new Uri($"http://localhost:19081/{appName}/{serviceName}/{controller}/{endpoint}");
//                 return uri;
//             }
//             else
//             {
//                 var port = ServicePorts.DevPortMapping[$"Vera.{serviceName}"];
//                 var url = $"http://localhost:{port}";

//                 var uri = new Uri(url + $"/{controller}/{endpoint}");
//                 return uri;
//             }
//         }

//         private static object CreateErrorModel(Type returnType, string errorMessage)
//         {
//             var error = new Error(ErrorCode.Exception, errorMessage);
//             var objectResponse = Activator.CreateInstance(returnType.GetGenericArguments()[0], new object[] { error });
//             if (objectResponse != null)
//             {
//                 return objectResponse;
//             }
//             else
//             {
//                 throw new Exception("Couldn't create instance of return type");
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

//         private static bool IsResponseWrapper(object objectToCheck)
//         {
//             var type = objectToCheck.GetType();
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


//         private static HttpRequestMessage CreateRequest(string token, Uri uri, HttpMethod method)
//         {
//             var request = new HttpRequestMessage
//             {
//                 RequestUri = uri,
//                 Method = method
//             };
//             if (!string.IsNullOrEmpty(token))
//             {
//                 request.Headers.Add("Authorization", token);
//             }

//             return request;
//         }


//         private async Task<HttpResponseMessage> PostData(Uri uri, object model, CurrentUser currentUser, JsonSerializerOptions jsonOptions)
//         {
//             var request = CreateRequest(currentUser.Token, uri, HttpMethod.Post);
//             var requestString = JsonSerializer.Serialize(model, jsonOptions);
//             var stringContent = new StringContent(requestString, Encoding.UTF8, "application/json");
//             request.Content = stringContent;

//             var result = await _httpClient.SendAsync(request);
//             return result;
//         }

//         private async Task<HttpResponseMessage> UploadFile(Uri uri, object model, CurrentUser currentUser)
//         {
//             var streamProperty = model.GetType().GetProperties().Single(f => typeof(Stream).IsAssignableFrom(f.PropertyType));
//             var stream = streamProperty.GetValue(model);

//             var request = CreateRequest(currentUser.Token, uri, HttpMethod.Post);
//             if (stream != null)
//             {
//                 var content = new StreamContent((Stream)stream);
//                 request.Content = content;
//             }

//             var serializedResult = JsonSerializer.Serialize(model, model.GetType());
//             request.Headers.Add(HeaderPrefix, serializedResult);

//             var result = await _httpClient.SendAsync(request);
//             return result;
//         }

//         private static async Task<T> DownloadFile<T>(HttpResponseMessage response) where T : class
//         {
//             if (response.Headers.TryGetValues(HeaderPrefix, out var result))
//             {
//                 var serializedResponse = response.Headers.GetValues(HeaderPrefix).First();
//                 var objectResponse = JsonSerializer.Deserialize<T>(serializedResponse) ?? throw new Exception("Couldn't deserialize response");

//                 var responseProperty = objectResponse.GetType()
//                     .GetProperty(nameof(ResponseWrapper<object>.Response)) ?? throw new Exception("Couldn't find response property on type");


//                 var streamProperty = responseProperty
//                     .PropertyType.GetProperties()
//                     .Single(f => typeof(Stream).IsAssignableFrom(f.PropertyType));

//                 var responseValue = responseProperty.GetValue(objectResponse);

//                 streamProperty.SetValue(responseValue, await response.Content.ReadAsStreamAsync());
//                 return objectResponse;
//             }

//             Error error;
//             if (response.StatusCode == HttpStatusCode.InternalServerError)
//             {
//                 error = new Error(ErrorCode.Exception, await response.Content.ReadAsStringAsync());
//             }
//             else if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
//             {
//                 error = new Error(ErrorCode.Unauthorized, await response.Content.ReadAsStringAsync());
//             }
//             else
//             {
//                 error = new Error(ErrorCode.InvalidInput, await response.Content.ReadAsStringAsync());
//             }

//             var errorResponse = Activator.CreateInstance(typeof(T), new object[] { error });
//             if (errorResponse != null)
//             {
//                 return (T)errorResponse;
//             }
//             else
//             {
//                 throw new Exception("Couldn't create instance of return type");
//             }
//         }

//         private static bool ContainsStream(Type type)
//         {
//             PropertyInfo[] properties;
//             if (typeof(ResponseWrapper).IsAssignableFrom(type))
//             {
//                 var a = type.GetProperty(nameof(ResponseWrapper<object>.Response));
//                 properties = a?.PropertyType.GetProperties() ?? Array.Empty<PropertyInfo>();
//             }
//             else
//             {
//                 properties = type.GetProperties();
//             }

//             return properties.Any(f => typeof(Stream).IsAssignableFrom(f.PropertyType));
//         }
//     }

// }
