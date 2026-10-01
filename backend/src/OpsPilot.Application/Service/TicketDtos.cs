using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Service;

namespace OpsPilot.Application.Service;

public record TicketListItemDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    string CustomerName,
    string? ProductCode,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    string? AssignedToName,
    int CommentCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record TicketCommentDto(Guid Id, string AuthorName, string Body, bool IsInternal, DateTime CreatedAtUtc);

public record TicketDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    string Description,
    Guid CustomerId,
    string CustomerName,
    Guid? SalesOrderId,
    string? OrderNumber,
    Guid? ProductId,
    string? ProductCode,
    string? ProductName,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    IReadOnlyList<TicketStatus> AllowedNextStatuses,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? Resolution,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    string? AiSummary,
    DateTime? AiSummaryAtUtc,
    IReadOnlyList<TicketCommentDto> Comments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record SaveTicketRequest(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid? SalesOrderId,
    Guid? ProductId,
    TicketCategory Category,
    TicketPriority Priority,
    Guid? AssignedToUserId);

public record ChangeTicketStatusRequest(TicketStatus Status, string? Resolution);

public record AddTicketCommentRequest(string Body, bool IsInternal);

public record TicketAssigneeDto(Guid Id, string Name, string Email);

/// <summary>Ticket counts for the list header: what's open, what's urgent, what's mine.</summary>
public record TicketStatsDto(int Open, int Urgent, int Unassigned, int AssignedToMe, int ResolvedLast30Days);

/// <param name="Content">Markdown from the model.</param>
public record TicketInsightsDto(string Content, int TicketsAnalysed, DateTime GeneratedAtUtc);

public class TicketQuery : PagedQuery
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public TicketCategory? Category { get; set; }
    public Guid? CustomerId { get; set; }

    /// <summary>Only tickets not yet resolved or closed.</summary>
    public bool OpenOnly { get; set; }
    public bool AssignedToMe { get; set; }
}
