using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Domain.Service;

/// <summary>A customer service ticket (design doc §13): prioritised, categorised, assigned, discussed and resolved.</summary>
public class ServiceTicket : BaseEntity
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Transitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.WaitingOnCustomer, TicketStatus.Resolved],
        [TicketStatus.InProgress] = [TicketStatus.WaitingOnCustomer, TicketStatus.Resolved],
        [TicketStatus.WaitingOnCustomer] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [TicketStatus.InProgress],
    };

    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    /// <summary>The product the ticket is about, when there is one (defects, installation, returns).</summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public TicketCategory Category { get; set; } = TicketCategory.General;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToName { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Last AI summary of the ticket history; regenerated on request, so it can lag behind new comments.</summary>
    public string? AiSummary { get; private set; }
    public DateTime? AiSummaryAtUtc { get; private set; }

    public ICollection<TicketComment> Comments { get; private set; } = new List<TicketComment>();

    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    public static IReadOnlyList<TicketStatus> AllowedNext(TicketStatus status) =>
        Transitions.TryGetValue(status, out var next) ? next : [];

    public void Assign(Guid? userId, string? name)
    {
        EnsureNotClosed("reassign");
        AssignedToUserId = userId;
        AssignedToName = userId is null ? null : name;
    }

    public void TransitionTo(TicketStatus next, DateTime nowUtc, string? resolution = null)
    {
        if (!AllowedNext(Status).Contains(next))
        {
            throw new BusinessRuleException($"Ticket {TicketNumber} cannot move from {Status} to {next}.");
        }

        if (next == TicketStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(resolution))
            {
                throw new BusinessRuleException("A resolution is required to resolve a ticket.");
            }
            Resolution = resolution.Trim();
            ResolvedAtUtc = nowUtc;
        }
        else if (next == TicketStatus.Closed)
        {
            ClosedAtUtc = nowUtc;
        }
        else if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            // Reopened: the earlier resolution no longer holds.
            Resolution = null;
            ResolvedAtUtc = null;
            ClosedAtUtc = null;
        }

        Status = next;
    }

    public TicketComment AddComment(string body, Guid? authorUserId, string authorName, bool isInternal, DateTime nowUtc)
    {
        EnsureNotClosed("comment on");
        var comment = new TicketComment
        {
            Body = body.Trim(),
            AuthorUserId = authorUserId,
            AuthorName = authorName,
            IsInternal = isInternal,
            CreatedAtUtc = nowUtc,
        };
        Comments.Add(comment);
        return comment;
    }

    public void SetAiSummary(string summary, DateTime nowUtc)
    {
        AiSummary = summary;
        AiSummaryAtUtc = nowUtc;
    }

    private void EnsureNotClosed(string action)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException($"Cannot {action} ticket {TicketNumber} while it is Closed; reopen it first.");
        }
    }
}

public class TicketComment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Internal notes are for staff only; the rest is the customer-facing conversation.</summary>
    public bool IsInternal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public enum TicketStatus
{
    Open,
    InProgress,
    WaitingOnCustomer,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TicketCategory
{
    General,
    Delivery,
    ProductDefect,
    Billing,
    Installation,
    Returns
}
