
using System.Threading.Tasks;
using Proxy;
using Proxy.NewProxy;

namespace Test.Interfaces
{

    public interface ServiceOne : IService
    {
        Task<Response<MethodClass>> MethodOne(MethodClass request);
        Task<Response<MethodClass>> MethodTwo(MethodClass request);
        Task<Response<MethodClass>> MethodThree(MethodClass request);
    }
}