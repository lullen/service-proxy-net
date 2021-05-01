
using System.Threading.Tasks;
using Google.Protobuf;
using Proxy.Models;

namespace Proxy.Client
{
    public interface IServiceProxy
    {
        Task<Response<TRes>> Invoke<T, TRes>(string app, string method, T request) 
            where T : class, IMessage, new()
            where TRes : class, IMessage, new();
    }
}