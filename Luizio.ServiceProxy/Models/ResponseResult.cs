using Microsoft.AspNetCore.Mvc;

namespace Luizio.ServiceProxy.Models;
public class ResponseResult<T, T2> : ObjectResult where T : Response<T2> where T2 : class
{

    public ResponseResult(Response<T2> value) : base(value.Result)
    {
        StatusCode = ToHttpStatusCode(value.Error);
        if (value.HasError)
        {
            Value = value.Error.Description;
        }

    }

    private static int ToHttpStatusCode(Error error)
    {
        return error.Code switch
        {
            ErrorCode.NotFound => 400,
            ErrorCode.Exception => 500,
            ErrorCode.Unauthorized => 403,
            ErrorCode.AlreadyExists => 400,
            ErrorCode.InvalidInput => 400,
            ErrorCode.Error => 500,
            _ => 200
        };
    }
}
