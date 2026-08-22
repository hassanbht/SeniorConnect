using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface ITrustSafetyDbContext
{
    DbSet<VolunteerApplication> VolunteerApplications { get; }
    DbSet<VolunteerApplicationStep> VolunteerApplicationSteps { get; }
    DbSet<BuddyAssignment> BuddyAssignments { get; }
    DbSet<KeyCustody> KeyCustodies { get; }
    DbSet<ExpenseRecord> ExpenseRecords { get; }
    DbSet<UserBlock> UserBlocks { get; }
    DbSet<SafeguardingCase> SafeguardingCases { get; }
    DbSet<SafeguardingCaseNote> SafeguardingCaseNotes { get; }
    DbSet<SafeguardingAccessLog> SafeguardingAccessLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
