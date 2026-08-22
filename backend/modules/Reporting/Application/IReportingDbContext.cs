using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

public interface IReportingDbContext
{
    DbSet<Funder> Funders { get; }
    DbSet<FundingRelationship> FundingRelationships { get; }
    DbSet<FunderMembership> FunderMemberships { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
