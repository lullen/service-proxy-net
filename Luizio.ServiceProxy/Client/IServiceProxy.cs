
using System.Threading.Tasks;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public interface IServiceProxy
{
    Task<Response<TRes>> Invoke<T, TRes>(string appName, string serviceName, string methodName, T request) 
        where T : class, new()
        where TRes : class, new();
}