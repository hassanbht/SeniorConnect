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

    public async Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var organizationIds = await _db.OrganizationMemberships
            .Where(m => m.UserId == userId
                     && m.Status == MembershipStatus.Active
                     && (m.Role == MembershipRole.Coordinator || m.Role == MembershipRole.Admin))
            .Select(m => m.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return await _db.Organizations
            .Where(o => organizationIds.Contains(o.Id))
            .Select(o => new StaffOrganizationDto(o.Id, o.Name))
            .ToListAsync(ct);
    }
}
