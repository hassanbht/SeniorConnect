using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class SupportProfileProvisioner : ISupportProfileProvisioner
{
    private readonly IProfilesDbContext _db;

    public SupportProfileProvisioner(IProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<Result> ProvisionSupportProfileAsync(
        Guid userId,
        string? postalCode,
        string? city,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        var existing = await _db.SupportProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (existing is not null)
        {
            return Result.Success();
        }

        var profile = SupportProfile.Create(
            userId: userId,
            addressLine: null,
            postalCode: postalCode,
            city: city,
            createdBy: createdByUserId);

        _db.SupportProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
