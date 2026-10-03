using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Contracts;

/// <summary>
/// The central location disclosure service
/// (docs/security/location-consent-and-visibility.md §4). Callers establish
/// the <see cref="LocationRelationship"/> from server-side facts; this service
/// adds the owner's current consent (read fresh on every call, so a
/// revocation applies to the very next request) and applies
/// <see cref="LocationVisibilityPolicy"/>.
/// </summary>
/// <remarks>
/// Phase 4: the viewer is part of every decision, so a block or a
/// safeguarding location restriction between owner and viewer yields
/// <see cref="LocationDisclosure.None"/>.
/// </remarks>
public interface ILocationVisibilityService
{
    Task<LocationDisclosure> DecideAsync(
        Guid ownerUserId, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct = default);

    /// <summary>Batch form for lists (discovery): one decision per owner, same viewer and relationship.</summary>
    Task<IReadOnlyDictionary<Guid, LocationDisclosure>> DecideManyAsync(
        IReadOnlyCollection<Guid> ownerUserIds, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct = default);
}
