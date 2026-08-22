using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public interface IHelpRequestsDbContext
{
    DbSet<Activity> Activities { get; }
    DbSet<ActivityCategory> ActivityCategories { get; }
    DbSet<ReferralDirectory> ReferralDirectories { get; }
    DbSet<HelpRequest> HelpRequests { get; }
    DbSet<HelpRequestStatusHistory> HelpRequestStatusHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
