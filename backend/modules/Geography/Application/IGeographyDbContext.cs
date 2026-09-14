using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Geography.Domain;

namespace SeniorConnect.Modules.Geography.Application;

public interface IGeographyDbContext
{
    DbSet<AustrianAdministrativeUnit> AustrianAdministrativeUnits { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}