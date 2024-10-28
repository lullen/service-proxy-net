
using System;

namespace Luizio.ServiceProxy.Server;


[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class SubscriberAttribute : Attribute
{
    public bool UseDeadLetterQueue { get; } = false;
    public int RetryCount { get; } = 3;


    public SubscriberAttribute()
    {
    }

    public SubscriberAttribute(int retryCount)
    {
        RetryCount = retryCount;
    }
    public SubscriberAttribute(bool useDeadLetterQueue, int retryCount)
    {
        UseDeadLetterQueue = useDeadLetterQueue;
        RetryCount = retryCount;
    }
}