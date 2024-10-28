
using System;
using System.Linq;
using System.Reflection;

namespace Luizio.ServiceProxy.Server;

internal class Subscription
{
    public MethodInfo? Method { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string PubSub { get; set; } = string.Empty;
    public string? DeadLetterQueue { get; set; }
    public string Service { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
}