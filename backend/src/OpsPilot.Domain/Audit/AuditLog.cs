namespace OpsPilot.Domain.Audit;

/// <summary>Append-only record of a consequential business action: who did what, to which record, and whether AI was involved.</summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public AuditSource Source { get; set; } = AuditSource.User;
    public string? DetailsJson { get; set; }
}

public enum AuditSource
{
    User,
    AiAssisted,
    System
}
