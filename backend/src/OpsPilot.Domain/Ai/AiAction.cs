using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Ai;

/// <summary>
/// An operation an AI agent proposed. Nothing happens until a person with the right role approves it;
/// execution then goes through the normal application services and business rules.
/// </summary>
public class AiAction : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid RequestedBy { get; set; }
    public string? RequestedByEmail { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public AiActionStatus Status { get; private set; } = AiActionStatus.Pending;

    /// <summary>The paused LangGraph run that resumes once a decision is made.</summary>
    public string ThreadId { get; set; } = string.Empty;

    public ICollection<AiApproval> Approvals { get; private set; } = new List<AiApproval>();

    public void Approve(Guid? approverId, string? approverEmail, string? comments, DateTime nowUtc)
    {
        EnsurePending();
        Status = AiActionStatus.Approved;
        Approvals.Add(new AiApproval
        {
            RequestedBy = RequestedBy, DecidedBy = approverId, DecidedByEmail = approverEmail, Status = AiApprovalStatus.Approved, Comments = comments, DecidedAtUtc = nowUtc,
        });
    }

    public void Reject(Guid? approverId, string? approverEmail, string? comments, DateTime nowUtc)
    {
        EnsurePending();
        Status = AiActionStatus.Rejected;
        Approvals.Add(new AiApproval
        {
            RequestedBy = RequestedBy, DecidedBy = approverId, DecidedByEmail = approverEmail, Status = AiApprovalStatus.Rejected, Comments = comments, DecidedAtUtc = nowUtc,
        });
    }

    public void MarkExecuted(string outputJson)
    {
        if (Status != AiActionStatus.Approved) throw new BusinessRuleException("Only approved actions can be executed.");
        Status = AiActionStatus.Executed;
        OutputJson = outputJson;
    }

    public void MarkFailed(string outputJson)
    {
        if (Status != AiActionStatus.Approved) throw new BusinessRuleException("Only approved actions can fail execution.");
        Status = AiActionStatus.Failed;
        OutputJson = outputJson;
    }

    private void EnsurePending()
    {
        if (Status != AiActionStatus.Pending)
        {
            throw new BusinessRuleException($"This action was already {Status.ToString().ToLowerInvariant()}.");
        }
    }
}

public class AiApproval
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? DecidedBy { get; set; }
    public string? DecidedByEmail { get; set; }
    public AiApprovalStatus Status { get; set; }
    public string? Comments { get; set; }
    public DateTime DecidedAtUtc { get; set; }
}

public enum AiActionStatus
{
    Pending,
    Approved,
    Rejected,
    Executed,
    Failed
}

public enum AiApprovalStatus
{
    Approved,
    Rejected
}

public static class AiActionTypes
{
    public const string CreatePurchaseOrders = nameof(CreatePurchaseOrders);
}
