using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Common.Observability;

namespace OpsPilot.Infrastructure.Ai;

public class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://localhost:8001";

    /// <summary>Shared secret the Python service requires on every call, so only this API can drive the agents.</summary>
    public string InternalKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;
}

public class AiAgentClient(HttpClient http, ILogger<AiAgentClient> logger) : IAiAgentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<AgentReply> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AgentChatRequest, AgentReply>("agent/chat", request, cancellationToken);

    public Task<AgentReply> ResumeAsync(AgentResumeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AgentResumeRequest, AgentReply>("agent/resume", request, cancellationToken);

    public Task<AiTextReply> SummarizeTicketAsync(TicketSummaryInput input, CancellationToken cancellationToken = default) =>
        PostAsync<TicketSummaryInput, AiTextReply>("tickets/summarize", input, cancellationToken);

    public Task<AiTextReply> FindRecurringProblemsAsync(TicketInsightsInput input, CancellationToken cancellationToken = default) =>
        PostAsync<TicketInsightsInput, AiTextReply>("tickets/insights", input, cancellationToken);

    public Task<InventoryScanReply> ScanInventoryAsync(AgentJobRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AgentJobRequest, InventoryScanReply>("jobs/inventory-scan", request, cancellationToken);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        using var activity = OpsPilotTelemetry.ActivitySource.StartActivity($"ai {path}", ActivityKind.Client);
        var started = Stopwatch.GetTimestamp();
        var outcome = "error";
        try
        {
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync(path, body, Json, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                outcome = "unreachable";
                throw new AiServiceUnavailableException($"Could not reach the AI service: {ex.Message}", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("AI service {Path} returned {Status}: {Detail}", path, (int)response.StatusCode, detail);
                outcome = $"http_{(int)response.StatusCode}";
                throw new AiServiceUnavailableException($"The AI service returned {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken)
                         ?? throw new AiServiceUnavailableException("The AI service returned an empty reply.");
            outcome = "ok";
            RecordAgentMetrics(path, result);
            return result;
        }
        finally
        {
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            OpsPilotTelemetry.AiRequestDuration.Record(seconds, OpsPilotTelemetry.Tags(("operation", path), ("outcome", outcome)));
            activity?.SetTag("opspilot.ai.outcome", outcome);
            if (outcome != "ok") activity?.SetStatus(ActivityStatusCode.Error, outcome);
        }
    }

    /// <summary>Token usage, agent time and tool outcomes, from the metadata the AI service returns with every reply.</summary>
    private void RecordAgentMetrics(string operation, object? result)
    {
        var metadata = result switch
        {
            AgentReply reply => reply.Metadata,
            AiTextReply text => text.Metadata,
            InventoryScanReply scan => scan.Metadata,
            _ => null,
        };
        if (metadata is not { ValueKind: JsonValueKind.Object } meta) return;

        var intent = result is AgentReply { Intent: { } i } ? i : operation;
        try
        {
            if (meta.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                foreach (var (property, direction) in new[] { ("input_tokens", "input"), ("output_tokens", "output") })
                {
                    if (usage.TryGetProperty(property, out var count) && count.TryGetInt64(out var tokens) && tokens > 0)
                    {
                        OpsPilotTelemetry.AiTokens.Add(tokens, OpsPilotTelemetry.Tags(("direction", direction), ("intent", intent)));
                    }
                }
            }
            if (meta.TryGetProperty("durationMs", out var ms) && ms.TryGetDouble(out var duration))
            {
                OpsPilotTelemetry.AgentExecutionDuration.Record(duration / 1000, OpsPilotTelemetry.Tags(("intent", intent)));
            }
            if (meta.TryGetProperty("trace", out var trace) && trace.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in trace.EnumerateArray())
                {
                    if (!step.TryGetProperty("kind", out var kind) || kind.GetString() != "tool") continue;
                    var tool = step.TryGetProperty("node", out var node) ? node.GetString() : "unknown";
                    var status = step.TryGetProperty("status", out var s) ? s.GetString() : "ok";
                    OpsPilotTelemetry.AgentToolCalls.Add(1, OpsPilotTelemetry.Tags(("tool", tool), ("outcome", status)));
                    if (status != "ok") logger.LogWarning("Agent tool {Tool} failed during {Intent}", tool, intent);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            logger.LogDebug(ex, "Unexpected AI metadata shape for {Operation}", operation);
        }
    }
}
