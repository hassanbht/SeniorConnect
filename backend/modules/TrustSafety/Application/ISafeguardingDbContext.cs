using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface ISafeguardingDbContext
{
    DbSet<SafeguardingCase> SafeguardingCases { get; }
    DbSet<SafeguardingCaseNote> SafeguardingCaseNotes { get; }
    DbSet<SafeguardingAccessLog> SafeguardingAccessLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
