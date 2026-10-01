using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Ai;

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

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(path, body, Json, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AiServiceUnavailableException($"Could not reach the AI service: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("AI service {Path} returned {Status}: {Detail}", path, (int)response.StatusCode, detail);
            throw new AiServiceUnavailableException($"The AI service returned {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken)
               ?? throw new AiServiceUnavailableException("The AI service returned an empty reply.");
    }
}
