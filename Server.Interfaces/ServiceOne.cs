
using System.Threading.Tasks;
using Luizio.iFX.Models;
using Luizio.iFX.Server;

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