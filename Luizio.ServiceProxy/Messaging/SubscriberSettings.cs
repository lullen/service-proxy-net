
using System;

namespace Luizio.ServiceProxy.Messaging;

public class SubscriberSettings
{
    public bool UseDeadLetterQueue { get; init; } = false;
    public int RetryCount { get; init; } = 3;
    public ushort PrefetchCount { get; init; } = 0;
    public string PubSub { get; set; } = string.Empty;
}