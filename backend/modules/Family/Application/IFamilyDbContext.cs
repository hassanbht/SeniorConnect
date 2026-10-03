using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Family.Domain;

namespace SeniorConnect.Modules.Family.Application;

public interface IFamilyDbContext
{
    DbSet<FamilyRelationship> FamilyRelationships { get; }
    DbSet<FamilyPermission> FamilyPermissions { get; }
    DbSet<SeniorAccessLog> SeniorAccessLogs { get; }
    DbSet<TrustedContact> TrustedContacts { get; }
    DbSet<SafetyAlert> SafetyAlerts { get; }
    DbSet<Zugangskarte> Zugangskarten { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
