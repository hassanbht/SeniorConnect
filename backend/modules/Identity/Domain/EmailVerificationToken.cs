using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

public sealed class EmailVerificationToken : Entity, IAuditable
{
    private EmailVerificationToken() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string TokenHash { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? UsedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsValid => UsedAtUtc == null && DateTimeOffset.UtcNow <= ExpiresAtUtc;

    public static EmailVerificationToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
    {
        var now = DateTimeOffset.UtcNow;
        return new EmailVerificationToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkUsed()
    {
        UsedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}