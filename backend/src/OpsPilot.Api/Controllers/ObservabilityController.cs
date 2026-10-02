using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Infrastructure;
using OpsPilot.Application.Common.Security;

namespace OpsPilot.Api.Controllers;

public record LatencySummary(string Name, long Count, long Failures, double P50Ms, double P95Ms, double MaxMs);

public record ToolSummary(string Tool, long Calls, long Failures);

public record ObservabilitySummary(
    DateTime SinceUtc,
    LatencySummary Http,
    IReadOnlyList<LatencySummary> SlowestRoutes,
    LatencySummary Database,
    IReadOnlyList<LatencySummary> AiOperations,
    IReadOnlyList<LatencySummary> AgentIntents,
    long InputTokens,
    long OutputTokens,
    IReadOnlyList<ToolSummary> Tools,
    IReadOnlyList<LatencySummary> Jobs);

/// <summary>
/// Live operational metrics since the API started (design doc §42): API latency, database command time, AI request and
/// agent time, token usage, tool failures and job runs. Full traces go to the OTLP backend when one is configured.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.ViewSystemHealth)]
[Route("api/observability")]
public class ObservabilityController(MetricsSnapshot metrics) : ControllerBase
{
    [HttpGet("summary")]
    public ObservabilitySummary Summary()
    {
        var http = metrics.Read("http.server.request.duration").Where(s => !s.Tag("http.route").StartsWith("health")).ToList();
        var routes = http
            .GroupBy(s => $"{s.Tag("http.request.method")} /{s.Tag("http.route")}")
            .Select(g => Combine(g.Key, g, s => s.Tag("http.response.status_code").StartsWith('5'), 1000))
            .OrderByDescending(r => r.P95Ms).Take(8).ToList();

        var tokens = metrics.Read("opspilot.ai.tokens");
        return new ObservabilitySummary(
            metrics.StartedAtUtc,
            Combine("All requests", http, s => s.Tag("http.response.status_code").StartsWith('5'), 1000),
            routes,
            Combine("Database commands", metrics.Read("opspilot.db.command.duration"), s => s.Tag("outcome") == "error", 1000),
            metrics.Read("opspilot.ai.request.duration").GroupBy(s => s.Tag("operation"))
                .Select(g => Combine(g.Key, g, s => s.Tag("outcome") != "ok", 1000)).OrderByDescending(r => r.Count).ToList(),
            metrics.Read("opspilot.agent.execution.duration").GroupBy(s => s.Tag("intent"))
                .Select(g => Combine(g.Key, g, _ => false, 1000)).OrderByDescending(r => r.Count).ToList(),
            (long)tokens.Where(t => t.Tag("direction") == "input").Sum(t => t.Sum),
            (long)tokens.Where(t => t.Tag("direction") == "output").Sum(t => t.Sum),
            metrics.Read("opspilot.agent.tool.calls").GroupBy(s => s.Tag("tool"))
                .Select(g => new ToolSummary(g.Key, (long)g.Sum(s => s.Sum), (long)g.Where(s => s.Tag("outcome") != "ok").Sum(s => s.Sum)))
                .OrderByDescending(t => t.Calls).ToList(),
            metrics.Read("opspilot.job.duration").GroupBy(s => s.Tag("job"))
                .Select(g => Combine(g.Key, g, s => s.Tag("outcome") != "ok", 1000)).ToList());
    }

    /// <summary>Merges series (e.g. one per status code) into one latency line. Percentiles take the worst series,
    /// which over-reports slightly but never hides a slow subset.</summary>
    private static LatencySummary Combine(string name, IEnumerable<SeriesView> series, Func<SeriesView, bool> isFailure, double scale)
    {
        var list = series.ToList();
        if (list.Count == 0) return new LatencySummary(name, 0, 0, 0, 0, 0);
        var count = list.Sum(s => s.Count);
        var p50 = list.Sum(s => s.P50 * s.Count) / count;
        return new LatencySummary(name, count, list.Where(isFailure).Sum(s => s.Count),
            Math.Round(p50 * scale, 1), Math.Round(list.Max(s => s.P95) * scale, 1), Math.Round(list.Max(s => s.Max) * scale, 1));
    }
}
