using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

/// <summary>The caller's own location-sharing consent (GET/PUT /api/v1/me/location-sharing).</summary>
public interface ILocationSharingService
{
    Task<Result<LocationSharingDto>> GetAsync(Guid userId, CancellationToken ct = default);

    Task<Result<LocationSharingDto>> SetAsync(Guid userId, UpdateLocationSharingRequest request, CancellationToken ct = default);
}

/// <summary>
/// <see cref="Level"/>: NONE | APPROXIMATE | PRECISE. <see cref="GrantedAtUtc"/>
/// is null for the default (never chosen). <see cref="CurrentConsentVersion"/>
/// is the explanation version a client must show and echo back.
/// </summary>
public sealed record LocationSharingDto(
    string Level,
    string? ConsentVersion,
    DateTimeOffset? GrantedAtUtc,
    string CurrentConsentVersion);

/// <summary><see cref="ConsentVersion"/> is the explanation version the person saw.</summary>
public sealed record UpdateLocationSharingRequest(string Level, string ConsentVersion);

public static class LocationSharingCodes
{
    public const string None = "NONE";
    public const string Approximate = "APPROXIMATE";
    public const string Precise = "PRECISE";

    public static string ToCode(LocationSharingLevel level) => level switch
    {
        LocationSharingLevel.Precise => Precise,
        LocationSharingLevel.Approximate => Approximate,
        _ => None,
    };

    public static LocationSharingLevel? Parse(string? code) => code switch
    {
        None => LocationSharingLevel.None,
        Approximate => LocationSharingLevel.Approximate,
        Precise => LocationSharingLevel.Precise,
        _ => null,
    };
}
