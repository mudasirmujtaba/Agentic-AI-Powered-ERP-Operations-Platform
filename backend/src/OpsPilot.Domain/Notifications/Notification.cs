namespace OpsPilot.Domain.Notifications;

/// <summary>An in-app message to one user, usually raised by a background job (design doc §40).</summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Short markdown body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>App route to open, e.g. <c>/finance/invoices?overdueOnly=true</c>.</summary>
    public string? Link { get; set; }

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>What raised it (e.g. <c>InventoryRiskScan</c>), for grouping and de-duplication.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public enum NotificationSeverity
{
    Info,
    Warning,
    Critical
}
