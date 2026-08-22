using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// Hashed, revocable per device. 90-day lifetime on a personal device (ADR-016 / BR-AUTH-04).
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string TokenHash { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string? DeviceLabel { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsPersonalDevice { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset IssuedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? ReplacedById { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive => RevokedAtUtc is null && DateTimeOffset.UtcNow < ExpiresAtUtc;

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        string? deviceLabel = null,
        bool isPersonalDevice = true,
        int lifetimeDays = 90)
    {
        var now = DateTimeOffset.UtcNow;
        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            DeviceLabel = deviceLabel,
            IsPersonalDevice = isPersonalDevice,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddDays(lifetimeDays)
        };
    }

    public void Revoke(Guid? replacedById = null)
    {
        RevokedAtUtc = DateTimeOffset.UtcNow;
        ReplacedById = replacedById;
    }
}
