using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface ITrustSafetyDbContext
{
    DbSet<VolunteerApplication> VolunteerApplications { get; }
    DbSet<VolunteerApplicationStep> VolunteerApplicationSteps { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
