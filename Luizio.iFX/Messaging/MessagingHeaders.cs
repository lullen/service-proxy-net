namespace Luizio.iFX.Messaging;

internal static class MessagingHeaders
{
    internal const string EventType = "x-event-type";

    internal const string RetryCount = "x-retry-count";

    internal const string TraceParent = "traceparent";

    internal const string TraceState = "tracestate";

    internal static readonly string[] All = [EventType, RetryCount, TraceParent, TraceState];
}
