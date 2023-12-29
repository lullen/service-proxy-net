using Luizio.ServiceProxy.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Common;
internal class CurrentUserMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException("next");

    public async Task Invoke(HttpContext context, CurrentUser currentUser)
    {
        InitCurrentUser(currentUser, context);
        await _next.Invoke(context);
    }


    private static void InitCurrentUser(CurrentUser currentUser, HttpContext context)
    {
        currentUser.Metadata = context.User.Claims.Select(c => KeyValuePair.Create<string, string>(c.Type, c.Value)).ToDictionary(c => c.Key, c => c.Value);
        if (context.Request.Headers.TryGetValue("Authorization", out var token))
        {
            currentUser.Token = token.ToString();
        }
    }
}
