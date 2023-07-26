
using System.Threading.Tasks;
using Proxy.Models;
using Proxy.Server;

namespace Server.Interfaces;


public interface ServiceOne : IService
{
    Task<Response<MethodResponseOne>> MethodOne(MethodRequestOne request);
    Task<Response<MethodResponseOne>> UploadFile(FileTestRequest request);
}

public interface ServiceTwo : IService
{
    Task<Response<MethodResponseTwo>> MethodTwo(MethodRequestTwo request);
    Task<Response<MethodResponseThree>> MethodThree(MethodRequestThree request);
}