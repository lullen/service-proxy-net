using System.Diagnostics.Metrics;

namespace Luizio.iFX;

public static class ProxyMeter
{
    public const string MeterName = "Luizio.iFX";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> ProxyDuration = Meter.CreateHistogram<double>(
        "proxy_invocation_duration_ms",
        unit: "ms",
        description: "Duration of proxy method invocations in milliseconds");
}
