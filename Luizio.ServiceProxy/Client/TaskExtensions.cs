
using System;
using System.Threading.Tasks;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Client;

public static class TaskExtensions
{
    public static async Task<Response<TRes>> Next<T, TRes>(this Task<Response<T>> task, Func<T, Task<Response<TRes>>> next)
    {
        var res = await task;
        if (res.HasError)
        {
            var constructor = typeof(Response<TRes>).GetConstructor(new[] { typeof(Error) });
            return (Response<TRes>)constructor!.Invoke(new[] { res.Error });
        }

        var nextRes = await next.Invoke(res.Result!);
        return nextRes;
    }

    public static async Task<Response<T>> OnError<T>(this Task<Response<T>> task, Func<Error, Task<Response<T>>> next)
    {
        var res = await task;
        if (res.HasError)
        {
            var nextRes = await next.Invoke(res.Error);
            return nextRes;
        }

        return res;
    }
}