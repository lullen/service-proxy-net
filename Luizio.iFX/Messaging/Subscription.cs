
using System;
using System.Threading.Tasks;
using Luizio.iFX.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.iFX.Messaging;

internal class Subscription
{
    public Func<IServiceProvider, CurrentUser, object, Task<Error>>? Invoker { get; set; }
    public Type EventType { get; set; } = typeof(object);
    public string MethodName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string PubSub { get; set; } = string.Empty;
    public string? DeadLetterQueue { get; set; }
    public string Service { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
    public ushort PrefetchCount { get; set; }
}