using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md): "people can find my
/// profile". Separate from location sharing. No row means not discoverable,
/// so nobody became findable when this was deployed.
/// </summary>
public sealed class DiscoveryPreference : Entity
{
    private DiscoveryPreference() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDiscoverable { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static DiscoveryPreference Create(Guid userId, bool isDiscoverable, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        IsDiscoverable = isDiscoverable,
        UpdatedAtUtc = now
    };

    public void Set(bool isDiscoverable, DateTimeOffset now)
    {
        IsDiscoverable = isDiscoverable;
        UpdatedAtUtc = now;
    }
}
