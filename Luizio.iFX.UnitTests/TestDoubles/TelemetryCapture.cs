using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Luizio.iFX.UnitTests.TestDoubles;

/// <summary>
/// Collects activities from an <see cref="ActivitySource"/> for the lifetime of the instance.
/// An ActivitySource with no listener returns null from StartActivity, so a test that wants to
/// observe spans must subscribe — exactly as the OpenTelemetry SDK does via AddSource.
/// </summary>
public sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener listener;
    public List<Activity> Activities { get; } = [];

    public ActivityCapture(string sourceName)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
    }

    public Activity Single(ActivityKind kind) => Activities.Single(a => a.Kind == kind);

    public void Dispose() => listener.Dispose();
}

public sealed record Measurement(string Instrument, long Value, Dictionary<string, object?> Tags)
{
    public string? Tag(string key) => Tags.TryGetValue(key, out var value) ? value?.ToString() : null;
}

/// <summary>Collects counter measurements from a meter for the lifetime of the instance.</summary>
public sealed class MetricCapture : IDisposable
{
    private readonly MeterListener listener;
    private readonly List<Measurement> measurements = [];

    public MetricCapture(string meterName)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName) l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var copied = new Dictionary<string, object?>();
            foreach (var tag in tags) copied[tag.Key] = tag.Value;
            lock (measurements) measurements.Add(new Measurement(instrument.Name, value, copied));
        });
        listener.Start();
    }

    public IReadOnlyList<Measurement> For(string instrument)
    {
        lock (measurements) return [.. measurements.Where(m => m.Instrument == instrument)];
    }

    public void Dispose() => listener.Dispose();
}
