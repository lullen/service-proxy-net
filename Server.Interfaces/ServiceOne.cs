
using System.Threading.Tasks;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;

namespace Server.Interfaces;


public interface ServiceOne : IService
{
    Task<Response<Empty>> MethodOne(MethodRequestOne request);
    Task<Response<MethodResponseOne>> UploadFile(FileTestRequest request);
}

public interface ServiceTwo : IService
{
    Task<Response<MethodResponseTwo>> MethodTwo(MethodRequestTwo request);
    Task<Response<MethodResponseThree>> MethodThree(MethodRequestThree request);
}