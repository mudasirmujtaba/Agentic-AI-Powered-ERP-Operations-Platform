using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Audit;

namespace OpsPilot.Application.Audit;

public record AuditLogDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string? UserEmail,
    string Action,
    string EntityType,
    string EntityId,
    string Summary,
    AuditSource Source,
    string? DetailsJson);

public class AuditLogQuery : PagedQuery
{
    public AuditSource? Source { get; set; }
    public string? EntityType { get; set; }
}

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}

public class AuditLogService(IApplicationDbContext db) : IAuditLogService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (query.Source is { } source) logs = logs.Where(l => l.Source == source);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) logs = logs.Where(l => l.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logs = logs.Where(l => l.Summary.Contains(search) || l.Action.Contains(search) ||
                                   l.EntityId.Contains(search) || (l.UserEmail != null && l.UserEmail.Contains(search)));
        }

        return await logs
            .OrderByDescending(l => l.OccurredAtUtc)
            .Select(l => new AuditLogDto(l.Id, l.OccurredAtUtc, l.UserEmail, l.Action, l.EntityType, l.EntityId, l.Summary, l.Source, l.DetailsJson))
            .ToPagedResultAsync(query, cancellationToken);
    }
}
