using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

/// <summary>
/// One entry of a person's location-sharing consent history
/// (docs/security/location-consent-and-visibility.md §2). Rows are never
/// edited except to revoke: a change revokes the current row and appends a
/// new one, so the history is the audit trail. No current row = None.
/// </summary>
public sealed class LocationSharingConsent : Entity
{
    /// <summary>Version of the explanation text the person agreed to. Bump when the wording changes.</summary>
    public const string CurrentVersion = "2026-09-location-v1";

    private LocationSharingConsent() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public LocationSharingLevel Level { get; private set; }

    [DataClass(DataClass.Operational)]
    public string ConsentVersion { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset GrantedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsCurrent => RevokedAtUtc is null;

    public static LocationSharingConsent Record(Guid userId, LocationSharingLevel level, DateTimeOffset nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Level = level,
        ConsentVersion = CurrentVersion,
        GrantedAtUtc = nowUtc.ToUniversalTime(),
    };

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc.ToUniversalTime();
}
