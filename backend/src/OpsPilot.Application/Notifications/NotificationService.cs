using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Notifications;

namespace OpsPilot.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    string? Link,
    NotificationSeverity Severity,
    string Source,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record NotificationFeedDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

/// <summary>The signed-in user's own notifications. Users only ever see and change their own rows.</summary>
public interface INotificationService
{
    Task<NotificationFeedDto> GetFeedAsync(int take, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}

public class NotificationService(IApplicationDbContext db, ICurrentUserService currentUser) : INotificationService
{
    public async Task<NotificationFeedDto> GetFeedAsync(int take, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var items = await mine
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 50))
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Link, n.Severity, n.Source, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return new NotificationFeedDto(items, await mine.CountAsync(n => n.ReadAtUtc == null, cancellationToken));
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), id);
        notification.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAtUtc == null).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        unread.ForEach(n => n.ReadAtUtc = now);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("Notifications need a signed-in user.");
}

/// <summary>Fans a message out to every active user in the given roles. Adds rows to the current unit of work.</summary>
public class NotificationPublisher(IApplicationDbContext db, IUserDirectory users)
{
    public async Task<int> NotifyRolesAsync(IEnumerable<string> roles, string title, string body, string? link,
        NotificationSeverity severity, string source, CancellationToken cancellationToken = default)
    {
        var recipients = await users.ActiveUserIdsInRolesAsync(roles, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var userId in recipients)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Link = link,
                Severity = severity,
                Source = source,
                CreatedAtUtc = now,
            });
        }
        return recipients.Count;
    }
}
