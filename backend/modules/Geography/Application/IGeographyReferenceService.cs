using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Geography.Application;

public interface IGeographyReferenceService
{
    Task<Result<IReadOnlyList<BundeslandDto>>> GetBundeslaenderAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BezirkDto>>> GetBezirkeAsync(string bundeslandCode, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GemeindeDto>>> GetGemeindenAsync(string bezirkCode, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GemeindeDto>>> LookupByPlzAsync(string plz, CancellationToken cancellationToken = default);
}

public sealed record BundeslandDto(string Code, string Name);

public sealed record BezirkDto(string Code, string Name);

public sealed record GemeindeDto(string Code, string Name, string PostalCode, double Latitude, double Longitude);

public interface IGeocodingProvider
{
    Task<Result<GeocodeResult>> GeocodeAsync(string address, string preferredLocale, CancellationToken cancellationToken = default);
}

public sealed record GeocodeResult(
    string AddressLine,
    string PostalCode,
    string City,
    double Latitude,
    double Longitude,
    string GemeindeCode,
    string GemeindeName,
    string BezirkCode,
    string BezirkName,
    string BundeslandCode,
    string BundeslandName,
    double Confidence);

public interface IProximityService
{
    Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyOrganizationsAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    /// <summary>
    /// Hotfix: the search origin is the caller's own stored profile location,
    /// never a client-supplied point. No stored location → LOCATION_UNAVAILABLE.
    /// Results carry area + distance band only — no coordinates, no km.
    /// </summary>
    Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyVolunteersAsync(
        Guid callerUserId, double radiusKm, CancellationToken ct = default);

    /// <inheritdoc cref="FindNearbyVolunteersAsync"/>
    Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyRequestsAsync(
        Guid callerUserId, double radiusKm, CancellationToken ct = default);

    Task<Result<IReadOnlyList<NearbyTownDto>>> GetNearestTownsAsync(
        double latitude, double longitude, double maxDistanceKm = 50, int maxResults = 10, CancellationToken ct = default);

    // P5-06: community events near a point, optionally restricted to the
    // caller's own saved interests (Interest.Code matched against the
    // event's Category — same free-text vocabulary).
    Task<Result<IReadOnlyList<CommunityEventDiscoveryResult>>> FindNearbyCommunityEventsAsync(
        double latitude, double longitude, double radiusKm, Guid? callerUserId, bool matchMyInterests, CancellationToken ct = default);
}

public sealed record CommunityEventDiscoveryResult(
    Guid EventId,
    string Title,
    string Category,
    DateTimeOffset StartsAtUtc,
    double DistanceKm);

/// <summary>
/// Organizations (public offices) carry exact coordinates and km. People and
/// requests (private homes) carry only area + <see cref="DistanceBand"/>:
/// DistanceKm, Latitude and Longitude are null for them.
/// </summary>
public sealed record ProximityResult(
    Guid UserId,
    string DisplayName,
    double? DistanceKm,
    string DistanceBand,
    string LocalityName,
    string PostalCode,
    string GemeindeName,
    string BezirkName,
    double? Latitude,
    double? Longitude,
    bool IsFuzzed);

public sealed record NearbyTownDto(
    string GemeindeCode,
    string GemeindeName,
    string BezirkName,
    string BundeslandName,
    double DistanceKm,
    double Latitude,
    double Longitude);

public interface IStoreLocationService
{
    Task<Result> StoreGeocodedLocationAsync(
        Guid userId,
        string addressLine,
        string postalCode,
        string city,
        double latitude,
        double longitude,
        string gemeindeCode,
        CancellationToken cancellationToken = default);
}