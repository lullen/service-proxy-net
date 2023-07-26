
using System;

namespace Proxy.Server;


[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class SubscriberAttribute : Attribute
{
    public string Topic { get; } = string.Empty;
    public SubscriberAttribute(string topic)
    {
        Topic = topic;
    }
}