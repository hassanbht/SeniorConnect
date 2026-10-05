using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

/// <summary>
/// ADR-029: "may manage staff two-factor" is an active Coordinator/Admin
/// membership in any organization — never the authentication method.
/// </summary>
public sealed class OrganizationStaffSecurityAuthorizer : IStaffSecurityAuthorizer
{
    private readonly IOrganizationsDbContext _db;

    public OrganizationStaffSecurityAuthorizer(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanManageStaffSecurityAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _db.OrganizationMemberships.AnyAsync(
            m => m.UserId == userId
              && m.Status == MembershipStatus.Active
              && (m.Role == MembershipRole.Coordinator || m.Role == MembershipRole.Admin),
            ct);
    }
}