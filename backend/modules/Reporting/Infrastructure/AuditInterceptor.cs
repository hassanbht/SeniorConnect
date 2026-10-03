using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
        {
            return base.SavingChanges(eventData, result);
        }

        AuditChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        AuditChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AuditChanges(DbContext dbContext)
    {
        var entries = dbContext.ChangeTracker.Entries().ToList();
        var auditEntries = new List<AuditEntry>();

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditEntry || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            string? action = null;
            if (entry.State == EntityState.Added)
            {
                action = "CREATE";
            }
            else if (entry.State == EntityState.Deleted)
            {
                action = "DELETE";
            }
            else if (entry.State == EntityState.Modified)
            {
                action = "UPDATE";
            }

            if (action is not null)
            {
                var entityType = entry.Entity.GetType();
                var subjectType = entityType.Name;
                
                // Get the Id property if it exists
                Guid? subjectId = null;
                if (entry.Entity is Entity entity)
                {
                    subjectId = entity.Id;
                }

                // If entity is ISoftDeletable and is being soft-deleted (IsDeleted changed from false to true)
                if (entry.Entity is ISoftDeletable && entry.State == EntityState.Modified)
                {
                    var isDeletedProp = entry.Property("IsDeleted");
                    if (isDeletedProp.IsModified && (bool)isDeletedProp.CurrentValue!)
                    {
                        action = "SOFT_DELETE";
                    }
                }

                var auditEntry = AuditEntry.Create(
                    action: $"{action}_{subjectType.ToUpperInvariant()}",
                    subjectType: subjectType,
                    subjectId: subjectId
                );
                auditEntries.Add(auditEntry);
            }
        }

        if (auditEntries.Count > 0)
        {
            dbContext.Set<AuditEntry>().AddRange(auditEntries);
        }
    }
}
