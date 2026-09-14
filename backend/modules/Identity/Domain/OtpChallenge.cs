using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// ADR-016 — OTP challenges. The primary credential for seniors and volunteers.
/// Nothing here stores a plaintext phone number, email address or code.
/// Hash the destination and the code, never store either (BR-AUTH-07).
/// </summary>
public sealed class OtpChallenge : Entity
{
    private OtpChallenge() { }

    [DataClass(DataClass.Operational)]
    public Guid? UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public OtpChannel Channel { get; private set; }

    /// <summary>Salted hash of the phone number or email — never plaintext.</summary>
    [DataClass(DataClass.Operational)]
    public string DestinationHash { get; private set; } = null!;

    /// <summary>Salted hash of the 6-digit code — never plaintext.</summary>
    [DataClass(DataClass.Operational)]
    public string CodeHash { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public OtpPurpose Purpose { get; private set; }

    [DataClass(DataClass.Operational)]
    public short Attempts { get; private set; }

    [DataClass(DataClass.Operational)]
    public short MaxAttempts { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? IpHash { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAtUtc;

    [DataClass(DataClass.Operational)]
    public bool IsConsumed => ConsumedAtUtc.HasValue;

    [DataClass(DataClass.Operational)]
    public bool IsExhausted => Attempts >= MaxAttempts;

    public static OtpChallenge Create(
        string destinationHash,
        string codeHash,
        OtpChannel channel,
        OtpPurpose purpose = OtpPurpose.Login,
        Guid? userId = null,
        string? ipHash = null,
        short maxAttempts = 5,
        int expiryMinutes = 5)
    {
        var now = DateTimeOffset.UtcNow;
        return new OtpChallenge
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Channel = channel,
            DestinationHash = destinationHash,
            CodeHash = codeHash,
            Purpose = purpose,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(expiryMinutes),
            IpHash = ipHash
        };
    }

    public Result RecordAttempt()
    {
        if (IsConsumed) return Error.InvalidStateTransition("consumed", "attempt");
        if (IsExpired) return Error.InvalidStateTransition("expired", "attempt");
        if (IsExhausted) return Error.InvalidStateTransition("exhausted", "attempt");

        Attempts++;
        return Result.Success();
    }

    public Result Consume()
    {
        if (IsConsumed) return Error.InvalidStateTransition("consumed", "consumed");
        if (IsExpired) return Error.InvalidStateTransition("expired", "consumed");
        if (IsExhausted) return Error.InvalidStateTransition("exhausted", "consumed");

        ConsumedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}

public enum OtpChannel
{
    Sms,
    Email
}

public enum OtpPurpose
{
    Login,
    Registration,
    PhoneChange,
    EmailChange,
    Recovery,
    PhoneVerification
}
