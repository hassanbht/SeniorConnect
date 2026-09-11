using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationCoordinatorReader : IOrganizationCoordinatorReader
{
    private readonly IOrganizationsDbContext _db;

    public OrganizationCoordinatorReader(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId
                     && m.Status == MembershipStatus.Active
                     && (m.Role == MembershipRole.Coordinator || m.Role == MembershipRole.Admin))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }
}
