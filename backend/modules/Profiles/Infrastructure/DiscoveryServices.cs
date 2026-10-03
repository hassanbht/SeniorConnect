using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class DiscoverabilityService(IProfilesDbContext db, TimeProvider? clock = null) : IDiscoverabilityService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<DiscoverabilityDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await db.DiscoveryPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        return Result<DiscoverabilityDto>.Success(new DiscoverabilityDto(pref?.IsDiscoverable ?? false, pref?.UpdatedAtUtc));
    }

    public async Task<Result<DiscoverabilityDto>> SetAsync(
        Guid userId, UpdateDiscoverabilityRequest request, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var pref = await db.DiscoveryPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (pref is null)
        {
            pref = DiscoveryPreference.Create(userId, request.IsDiscoverable, now);
            db.DiscoveryPreferences.Add(pref);
        }
        else if (pref.IsDiscoverable != request.IsDiscoverable)
        {
            pref.Set(request.IsDiscoverable, now);
        }
        await db.SaveChangesAsync(ct);
        return Result<DiscoverabilityDto>.Success(new DiscoverabilityDto(pref.IsDiscoverable, pref.UpdatedAtUtc));
    }
}

/// <summary>
/// Phase 4: every rule for "may this person appear?" in one place. All inputs
/// are read fresh from persisted facts; nothing the client sends is used.
/// </summary>
public sealed class DiscoveryEligibilityService(
    IProfilesDbContext db,
    IVerificationFactsReader facts,
    ISafetyBoundaryReader blocks,
    ISafeguardingRestrictionReader restrictions,
    IOrganizationDirectory directory) : IDiscoveryEligibilityService
{
    /// <summary>
    /// Decision V-1 (docs/security/verification-and-discovery.md §6): open
    /// discovery needs contact verification (email and phone, BR-TRUST-02
    /// "Contact Verified") or an organization verification. Address
    /// verification is not required just to be listed; the safety level of an
    /// activity still gates matching (BR-SAFETY-03).
    /// </summary>
    public const VerificationLevel MinimumSelfVerification = VerificationLevel.EmailPhone;

    public async Task<IReadOnlySet<Guid>> FilterEligibleAsync(
        Guid counterpartUserId,
        IReadOnlyCollection<Guid> candidateUserIds,
        DiscoveryContext context,
        CancellationToken ct = default)
    {
        var ids = candidateUserIds.Where(id => id != counterpartUserId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var blocked = (await blocks.GetBlockedUserIdsAsync(counterpartUserId, ids, ct)).ToHashSet();
        var active = await restrictions.GetActiveAsync([counterpartUserId, .. ids], ct);
        var remaining = ids
            .Where(id => !blocked.Contains(id))
            .Where(id => !active.Restricts(RestrictionKind.Discovery, id, counterpartUserId))
            .Where(id => context.Purpose != DiscoveryPurpose.Matching
                         || !active.Restricts(RestrictionKind.Assignment, id, counterpartUserId))
            .ToList();

        if (context.Purpose == DiscoveryPurpose.OwnedContent || remaining.Count == 0)
        {
            return remaining.ToHashSet();
        }

        var verified = await facts.GetManyAsync(remaining, ct);
        var discoverable = (await db.DiscoveryPreferences.AsNoTracking()
                .Where(p => remaining.Contains(p.UserId) && p.IsDiscoverable)
                .Select(p => p.UserId)
                .ToListAsync(ct))
            .ToHashSet();
        var roster = context.Purpose == DiscoveryPurpose.Matching && context.OrganizationId is { } org
            ? (await directory.GetActiveVolunteerUserIdsAsync(org, ct)).ToHashSet()
            : [];

        return remaining
            .Where(id => verified[id].IsActiveAccount)
            .Where(id => roster.Contains(id) || (discoverable.Contains(id) && MeetsOpenVerification(verified[id])))
            .ToHashSet();
    }

    /// <summary>
    /// Organization rule: an active volunteer of the request's own
    /// organization is a candidate for that organization's matching without
    /// the public discovery switch. Everyone else must have chosen to be
    /// discoverable and be verified.
    /// </summary>
    public static bool MeetsOpenVerification(VerificationFacts f) =>
        f.OrganizationVerified || f.Level >= MinimumSelfVerification;
}

/// <summary>
/// The central disclosure decision: current consent (never cached) plus
/// <see cref="LocationVisibilityPolicy"/>. Phase 4: a block between owner
/// and viewer, or a safeguarding location restriction, withholds the location
/// entirely. Nothing lifts it: not verification, not a staff or officer role.
/// </summary>
public sealed class LocationVisibilityService(
    IProfilesDbContext db,
    ISafetyBoundaryReader blocks,
    ISafeguardingRestrictionReader restrictions) : ILocationVisibilityService
{
    public async Task<LocationDisclosure> DecideAsync(
        Guid ownerUserId, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct = default) =>
        (await DecideManyAsync([ownerUserId], viewerUserId, relationship, ct))[ownerUserId];

    public async Task<IReadOnlyDictionary<Guid, LocationDisclosure>> DecideManyAsync(
        IReadOnlyCollection<Guid> ownerUserIds, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct = default)
    {
        var ids = ownerUserIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, LocationDisclosure>();
        }

        var levels = (await db.LocationSharingConsents.AsNoTracking()
                .Where(c => ids.Contains(c.UserId) && c.RevokedAtUtc == null)
                .Select(c => new { c.UserId, c.Level, c.GrantedAtUtc })
                .ToListAsync(ct))
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => c.GrantedAtUtc)!.Level);
        var blocked = (await blocks.GetBlockedUserIdsAsync(viewerUserId, ids, ct)).ToHashSet();
        var active = await restrictions.GetActiveAsync([viewerUserId, .. ids], ct);

        return ids.ToDictionary(id => id, id =>
            id != viewerUserId && (blocked.Contains(id) || active.Restricts(RestrictionKind.Location, id, viewerUserId))
                ? LocationDisclosure.None
                : LocationVisibilityPolicy.Decide(relationship, levels.GetValueOrDefault(id, LocationSharingLevel.None)));
    }
}
