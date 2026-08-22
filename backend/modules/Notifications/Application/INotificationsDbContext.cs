using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Modules.Notifications.Application;

public interface INotificationsDbContext
{
    DbSet<NotificationMessage> NotificationMessages { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<NotificationBudgetTracker> NotificationBudgetTrackers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
