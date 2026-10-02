using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpsPilot.Application.Common.Observability;

/// <summary>
/// The platform's own traces and metrics (design doc §42), on the BCL APIs so the Application layer stays free of
/// exporter dependencies. The API host wires these names into OpenTelemetry.
/// </summary>
public static class OpsPilotTelemetry
{
    public const string Name = "OpsPilot";

    public static readonly ActivitySource ActivitySource = new(Name);
    private static readonly Meter Meter = new(Name);

    /// <summary>Round trip to the AI service, by operation (chat, resume, ticket summary...) and outcome.</summary>
    public static readonly Histogram<double> AiRequestDuration =
        Meter.CreateHistogram<double>("opspilot.ai.request.duration", "s", "Duration of calls to the AI service.");

    public static readonly Counter<long> AiTokens =
        Meter.CreateCounter<long>("opspilot.ai.tokens", "{token}", "LLM tokens used, by direction (input/output).");

    /// <summary>Time spent inside one agent run, as reported by the agent, by intent.</summary>
    public static readonly Histogram<double> AgentExecutionDuration =
        Meter.CreateHistogram<double>("opspilot.agent.execution.duration", "s", "Agent execution time reported by the AI service.");

    public static readonly Counter<long> AgentToolCalls =
        Meter.CreateCounter<long>("opspilot.agent.tool.calls", "{call}", "Agent tool calls, by tool and outcome.");

    public static readonly Histogram<double> DbCommandDuration =
        Meter.CreateHistogram<double>("opspilot.db.command.duration", "s", "Database command duration, by command kind.");

    public static readonly Histogram<double> JobDuration =
        Meter.CreateHistogram<double>("opspilot.job.duration", "s", "Background job duration, by job and outcome.");

    public static TagList Tags(params (string Key, object? Value)[] tags)
    {
        var list = new TagList();
        foreach (var (key, value) in tags) list.Add(key, value);
        return list;
    }
}
