using Microsoft.AspNetCore.Mvc;

namespace Luizio.ServiceProxy.Models;
public class ResponseResult<T> : ObjectResult where T : class
{

    public ResponseResult(Response<T> value) : base(value.Result)
    {
        StatusCode = ToHttpStatusCode(value.Error);
        if (value.HasError)
        {
            Value = value.Error.Description;
        }
    }

    public ResponseResult(Error value) : base(value)
    {
        StatusCode = ToHttpStatusCode(value);
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
