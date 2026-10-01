using System.Text.Json;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Audit;

namespace OpsPilot.Application.Audit;

/// <summary>Adds audit entries to the current unit of work, so they commit (or roll back) with the change they describe.</summary>
public class AuditLogWriter(IApplicationDbContext db, ICurrentUserService currentUser)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Record(string action, string entityType, string entityId, string summary,
        AuditSource source = AuditSource.User, object? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = currentUser.UserId,
            UserEmail = currentUser.Email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            Source = source,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
        });
    }
}
