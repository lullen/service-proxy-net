
using System.Threading.Tasks;
using Proxy;
using Proxy.NewProxy;

namespace Test.Interfaces
{

    public interface ServiceOne : IService
    {
        Task<Response<MethodClass>> MethodOne(MethodClass request);
    }
    
    public interface ServiceTwo : IService
    {
        Task<Response<MethodClass2>> MethodTwo(MethodClass2 request);
        Task<Response<MethodClass3>> MethodThree(MethodClass3 request);
    }
}