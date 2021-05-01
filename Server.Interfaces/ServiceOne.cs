
using System.Threading.Tasks;
using Proxy;
using Proxy.NewProxy;
using Proxy.Server;

namespace Server.Interfaces
{

    [ExposedService]
    public interface ServiceOne : IService
    {
        
        Task<Response<MethodResponseOne>> MethodOne(MethodRequestOne request);
    }
    
    [ExposedService]
    public interface ServiceTwo : IService
    {
        Task<Response<MethodResponseTwo>> MethodTwo(MethodRequestTwo request);
        Task<Response<MethodResponseThree>> MethodThree(MethodRequestThree request);
    }
}