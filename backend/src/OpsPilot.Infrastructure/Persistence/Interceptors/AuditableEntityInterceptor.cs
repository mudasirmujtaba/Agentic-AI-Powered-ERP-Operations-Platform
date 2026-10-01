using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Common;

namespace OpsPilot.Infrastructure.Persistence.Interceptors;

public class AuditableEntityInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;
        var parentsWithChangedChildren = FindParentsWithChangedChildren(context);

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // An explicit value (demo data backdating history) is kept; otherwise stamp now.
                if (entry.Entity.CreatedAtUtc == default) entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified ||
                     (entry.State == EntityState.Unchanged && parentsWithChangedChildren.Contains(entry.Entity.Id)))
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }

    // Editing only a child collection (e.g. a customer's addresses) leaves the parent row Unchanged,
    // but the parent was still edited, so it should be stamped too.
    private static HashSet<Guid> FindParentsWithChangedChildren(DbContext context)
    {
        var parentIds = new HashSet<Guid>();

        var changedChildren = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not BaseEntity &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (var child in changedChildren)
        {
            foreach (var foreignKey in child.Metadata.GetForeignKeys())
            {
                if (!typeof(BaseEntity).IsAssignableFrom(foreignKey.PrincipalEntityType.ClrType) ||
                    foreignKey.Properties.Count != 1)
                {
                    continue;
                }

                var property = child.Property(foreignKey.Properties[0].Name);
                var value = child.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
                if (value is Guid parentId)
                {
                    parentIds.Add(parentId);
                }
            }
        }

        return parentIds;
    }
}
