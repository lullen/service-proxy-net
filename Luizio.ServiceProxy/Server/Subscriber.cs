
using System;

namespace Luizio.ServiceProxy.Server;


[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class SubscriberAttribute : Attribute
{
    public bool UseDeadLetterQueue { get; }

    public SubscriberAttribute(bool useDeadLetterQueue)
    {
        UseDeadLetterQueue = useDeadLetterQueue;
    }
}