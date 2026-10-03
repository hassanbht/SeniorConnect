using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

/// <summary>Reads and changes the caller's own consent. Only ever for the authenticated user.</summary>
public sealed class LocationSharingService(IProfilesDbContext db, TimeProvider? clock = null) : ILocationSharingService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<LocationSharingDto>> GetAsync(Guid userId, CancellationToken ct = default) =>
        Result<LocationSharingDto>.Success(Map(await CurrentAsync(db, userId, ct)));

    public async Task<Result<LocationSharingDto>> SetAsync(
        Guid userId, UpdateLocationSharingRequest request, CancellationToken ct = default)
    {
        if (LocationSharingCodes.Parse(request.Level) is not { } level)
        {
            return new Error("LOCATION_SHARING_LEVEL_INVALID", "Level must be NONE, APPROXIMATE or PRECISE.", ErrorKind.Validation);
        }

        // Consent counts only for the explanation actually shown. Turning
        // sharing off needs no explanation, so it never fails on version.
        if (level != LocationSharingLevel.None && request.ConsentVersion != LocationSharingConsent.CurrentVersion)
        {
            return new Error(
                "CONSENT_VERSION_OUTDATED",
                "The explanation shown is out of date. Reload the setting and confirm again.",
                ErrorKind.Conflict,
                new Dictionary<string, object> { ["currentConsentVersion"] = LocationSharingConsent.CurrentVersion });
        }

        var current = await CurrentAsync(db, userId, ct, tracked: true);
        if ((current?.Level ?? LocationSharingLevel.None) == level)
        {
            return Result<LocationSharingDto>.Success(Map(current)); // unchanged: no new history row
        }

        var now = _clock.GetUtcNow();
        if (current is not null)
        {
            // Saved first so the "one current row" unique index never sees
            // two. If the insert below then fails, the person is left at
            // None — the safe side.
            current.Revoke(now);
            await db.SaveChangesAsync(ct);
        }

        var next = LocationSharingConsent.Record(userId, level, now);
        db.LocationSharingConsents.Add(next);
        await db.SaveChangesAsync(ct);

        return Result<LocationSharingDto>.Success(Map(next));
    }

    internal static Task<LocationSharingConsent?> CurrentAsync(
        IProfilesDbContext db, Guid userId, CancellationToken ct, bool tracked = false)
    {
        var query = db.LocationSharingConsents.Where(c => c.UserId == userId && c.RevokedAtUtc == null);
        return (tracked ? query : query.AsNoTracking())
            .OrderByDescending(c => c.GrantedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    private static LocationSharingDto Map(LocationSharingConsent? consent) => new(
        Level: LocationSharingCodes.ToCode(consent?.Level ?? LocationSharingLevel.None),
        ConsentVersion: consent?.ConsentVersion,
        GrantedAtUtc: consent?.GrantedAtUtc,
        CurrentConsentVersion: LocationSharingConsent.CurrentVersion);
}
