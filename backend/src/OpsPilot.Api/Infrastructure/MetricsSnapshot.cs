using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using OpsPilot.Application.Common.Observability;

namespace OpsPilot.Api.Infrastructure;

/// <summary>
/// In-process aggregation of the platform's metrics since start-up, for the System health report. It complements
/// (doesn't replace) OTLP export: it works with no collector running, which suits a single-node deployment.
/// Each series keeps exact counts plus a bounded sample for percentiles.
/// </summary>
public sealed class MetricsSnapshot : IHostedService, IDisposable
{
    private const int SampleSize = 2048;
    private readonly ConcurrentDictionary<(string Instrument, string Key), Series> _series = new();
    private readonly MeterListener _listener = new();
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OpsPilotTelemetry.Name ||
                (instrument.Meter.Name == "Microsoft.AspNetCore.Hosting" && instrument.Name == "http.server.request.duration"))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var key = string.Join('|', KeyTags(instrument.Name, tags));
        if (!_series.TryGetValue((instrument.Name, key), out var series))
        {
            series = _series.GetOrAdd((instrument.Name, key), new Series(KeyDictionary(instrument.Name, tags)));
        }
        series.Add(value);
    }

    /// <summary>The tags each instrument is grouped by; everything else is ignored to keep cardinality low.</summary>
    private static string[] GroupBy(string instrument) => instrument switch
    {
        "http.server.request.duration" => ["http.route", "http.request.method", "http.response.status_code"],
        "opspilot.ai.request.duration" => ["operation", "outcome"],
        "opspilot.ai.tokens" => ["direction"],
        "opspilot.agent.execution.duration" => ["intent"],
        "opspilot.agent.tool.calls" => ["tool", "outcome"],
        "opspilot.db.command.duration" => ["outcome"],
        "opspilot.job.duration" => ["job", "outcome"],
        _ => [],
    };

    private static IEnumerable<string> KeyTags(string instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var wanted = GroupBy(instrument);
        var values = new string[wanted.Length];
        foreach (var tag in tags)
        {
            var index = Array.IndexOf(wanted, tag.Key);
            if (index >= 0) values[index] = tag.Value?.ToString() ?? "";
        }
        return values;
    }

    private static Dictionary<string, string> KeyDictionary(string instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var wanted = GroupBy(instrument);
        var result = wanted.ToDictionary(w => w, _ => "");
        foreach (var tag in tags)
        {
            if (result.ContainsKey(tag.Key)) result[tag.Key] = tag.Value?.ToString() ?? "";
        }
        return result;
    }

    public IReadOnlyList<SeriesView> Read(string instrument) =>
        _series.Where(s => s.Key.Instrument == instrument).Select(s => s.Value.View()).ToList();

    public sealed class Series(Dictionary<string, string> tags)
    {
        private readonly double[] _sample = new double[SampleSize];
        private readonly Lock _lock = new();
        private long _count;
        private double _sum;
        private double _max;

        public void Add(double value)
        {
            lock (_lock)
            {
                _sample[_count % SampleSize] = value;
                _count++;
                _sum += value;
                _max = Math.Max(_max, value);
            }
        }

        public SeriesView View()
        {
            lock (_lock)
            {
                var n = (int)Math.Min(_count, SampleSize);
                var sorted = _sample.Take(n).Order().ToArray();
                return new SeriesView(tags, _count, _sum, _max, Percentile(sorted, 0.5), Percentile(sorted, 0.95));
            }
        }

        private static double Percentile(double[] sorted, double p) =>
            sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];
    }
}

public record SeriesView(IReadOnlyDictionary<string, string> Tags, long Count, double Sum, double Max, double P50, double P95)
{
    public string Tag(string key) => Tags.TryGetValue(key, out var value) ? value : "";
}
