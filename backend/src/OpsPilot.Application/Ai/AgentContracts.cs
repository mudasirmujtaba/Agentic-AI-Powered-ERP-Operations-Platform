using System.Text.Json;

namespace OpsPilot.Application.Ai;

/// <summary>Client for the Python LangGraph service. The ERP stays the trusted layer; the agent only reads through the user's own token.</summary>
public interface IAiAgentClient
{
    Task<AgentReply> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default);
    Task<AgentReply> ResumeAsync(AgentResumeRequest request, CancellationToken cancellationToken = default);

    /// <summary>One-shot text tasks over data the ERP has already authorised and passes in (no agent tools, no checkpoint).</summary>
    Task<AiTextReply> SummarizeTicketAsync(TicketSummaryInput input, CancellationToken cancellationToken = default);
    Task<AiTextReply> FindRecurringProblemsAsync(TicketInsightsInput input, CancellationToken cancellationToken = default);
}

public record AiTextReply(string Content, JsonElement? Metadata);

public record TicketHistoryEntry(string Author, string Body, bool IsInternal, DateTime AtUtc);

public record TicketSummaryInput(
    string TicketNumber,
    string Subject,
    string Description,
    string Customer,
    string Category,
    string Priority,
    string Status,
    string? Resolution,
    IReadOnlyList<TicketHistoryEntry> History);

public record TicketBrief(string TicketNumber, string Subject, string Description, string Customer, string Category,
    string Priority, string Status, string? Product, string? Resolution, DateTime CreatedAtUtc);

public record TicketInsightsInput(int Days, IReadOnlyList<TicketBrief> Tickets);

public record AgentUser(Guid Id, string Email, string Name, IReadOnlyList<string> Roles);

public record AgentHistoryMessage(string Role, string Content);

/// <param name="AccessToken">The caller's JWT, so every ERP read the agent makes is subject to the caller's own permissions.</param>
public record AgentChatRequest(
    string ThreadId,
    string Message,
    IReadOnlyList<AgentHistoryMessage> History,
    AgentUser User,
    string AccessToken);

public record AgentResumeRequest(
    string ThreadId,
    string Decision,
    string? Comments,
    JsonElement Outcome,
    AgentUser User,
    string AccessToken);

public record AgentProposal(string ActionType, string Agent, string Summary, JsonElement Payload);

/// <param name="Metadata">Opaque structured output (intent, data table, SQL, citations, node trace, token usage) stored with the message.</param>
public record AgentReply(string Content, string? Intent, AgentProposal? Proposal, JsonElement? Metadata);

public class AiServiceUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
