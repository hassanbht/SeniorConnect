using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Contracts;
using SeniorConnect.Modules.Family.Domain;

namespace SeniorConnect.Modules.Family.Infrastructure;

public sealed class FamilyPermissionReader : IFamilyPermissionReader
{
    private readonly IFamilyDbContext _db;

    public FamilyPermissionReader(IFamilyDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(
        Guid caregiverUserId,
        Guid seniorUserId,
        string permissionType,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<PermissionType>(permissionType, ignoreCase: true, out var parsedType))
        {
            return false;
        }

        var relationship = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.SeniorUserId == seniorUserId
                && r.CaregiverUserId == caregiverUserId
                && r.Status == RelationshipStatus.Active, ct);

        if (relationship is null)
        {
            return false;
        }

        return relationship.HasPermission(parsedType);
    }
}
