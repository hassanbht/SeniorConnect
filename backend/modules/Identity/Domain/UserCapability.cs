using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// Capability-based authorization (docs/architecture/authorization.md).
/// Derived capabilities are recomputed; granted ones are inserted by staff.
/// </summary>
public sealed class UserCapability : Entity
{
    private UserCapability() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Capability { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public CapabilitySource Source { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? GrantedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? GrantedByOrgId { get; private set; }

    /// <summary>Required when source is 'granted'.</summary>
    [DataClass(DataClass.Operational)]
    public string? Reason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset GrantedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTimeOffset.UtcNow > ExpiresAtUtc.Value;

    public static UserCapability Derived(
        Guid userId,
        string capability)
    {
        return new UserCapability
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Capability = capability,
            Source = CapabilitySource.Derived,
            GrantedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static UserCapability Granted(
        Guid userId,
        string capability,
        Guid grantedByUserId,
        string reason,
        Guid? grantedByOrgId = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        return new UserCapability
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Capability = capability,
            Source = CapabilitySource.Granted,
            GrantedByUserId = grantedByUserId,
            GrantedByOrgId = grantedByOrgId,
            Reason = reason,
            GrantedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };
    }
}

public enum CapabilitySource
{
    Derived,
    Granted
}
