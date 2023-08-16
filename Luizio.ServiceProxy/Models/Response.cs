using System;
using System.Text.Json.Serialization;

namespace Luizio.ServiceProxy.Models;

public class Response<T> where T : class
{
    public Error Error { get; set; } = Error.Empty;
    public T? Result { get; set; }

    public Response(T response)
    {
        Result = response;
    }

    public Response(Error error)
    {
        Error = error;
    }

    public Response(Error error, T response)
    {
        Error = error;
        Result = response;
    }

    public Response()
    {

    }

    public bool HasError
    {
        get
        {
            return Error.HasError || Result == null;
        }
    }

    [JsonIgnore]
    public bool AlreadyExists
    {
        get
        {
            return HasError && Error.Code == ErrorCode.AlreadyExists;
        }
    }

    public Response<TRes> Next<TRes>(Func<Response<TRes>> next) where TRes : class, new()
    {
        if (HasError)
        {
            return Error;
        }
        return next.Invoke();
    }

    public Response<T> OnError(Func<Error, Response<T>> onError)
    {
        if (!HasError)
        {
            return this;
        }
        return onError.Invoke(Error);
    }


    public static implicit operator Response<T>(Error value)
    {
        return new Response<T>(value);
    }

    public static implicit operator Response<T>(T value)
    {
        return new Response<T>(value);
    }
}
