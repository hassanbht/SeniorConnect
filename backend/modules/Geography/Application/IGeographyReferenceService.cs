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

    Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyVolunteersAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyRequestsAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default);

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

public sealed record ProximityResult(
    Guid UserId,
    string DisplayName,
    double DistanceKm,
    string LocalityName,
    string PostalCode,
    string GemeindeName,
    string BezirkName,
    double Latitude,
    double Longitude,
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