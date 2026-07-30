using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Luizio.iFX.Messaging;

public static class MessagingActivitySource
{
    public const string SourceName = "Luizio.iFX.Messaging";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}

public static class MessagingMeter
{
    public const string MeterName = "Luizio.iFX.Messaging";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> EventsPublished = Meter.CreateCounter<long>(
        "messaging_events_published",
        unit: "{event}",
        description: "Events published, tagged by exchange.");

    public static readonly Counter<long> EventsConsumed = Meter.CreateCounter<long>(
        "messaging_events_consumed",
        unit: "{event}",
        description: "Event deliveries handled, tagged by queue, event type and outcome (processed, retried, dead_lettered, discarded).");
}

internal static class MessagingOutcome
{
    internal const string Processed = "processed";
    internal const string Retried = "retried";
    internal const string DeadLettered = "dead_lettered";
    internal const string Discarded = "discarded";

    internal const string UnknownEventType = "unknown";
}
