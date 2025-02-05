
using System;

namespace Luizio.ServiceProxy.Server;


[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class SubscriberAttribute : Attribute
{
    public bool UseDeadLetterQueue { get; } = false;
    public int RetryCount { get; } = 3;
    public ushort PrefetchCount { get; } = 0;


    public SubscriberAttribute()
    {
    }

    public SubscriberAttribute(int retryCount)
    {
        RetryCount = retryCount;
    }
    public SubscriberAttribute(bool useDeadLetterQueue, int retryCount, ushort prefetchCount)
    {
        UseDeadLetterQueue = useDeadLetterQueue;
        RetryCount = retryCount;
        PrefetchCount = prefetchCount;
    }
}