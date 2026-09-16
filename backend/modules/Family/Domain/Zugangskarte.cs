using System.Security.Cryptography;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Family.Domain;

public sealed class Zugangskarte
{
    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public Guid CreatedByCaregiverUserId { get; private set; }
    public string PairingCode { get; private set; } = string.Empty; // 6-digit numeric code
    public string QrToken { get; private set; } = string.Empty;
    public string QrPayload { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ClaimedAtUtc { get; private set; }
    public Guid? ClaimedByUserId { get; private set; }
    public short FailedAttempts { get; private set; }
    public short MaxAttempts { get; private set; } = 5;

    public bool IsClaimed => ClaimedAtUtc.HasValue;
    public bool IsExhausted => FailedAttempts >= MaxAttempts;

    private Zugangskarte() { }

    public static Zugangskarte Generate(Guid seniorUserId, Guid createdByCaregiverUserId, TimeSpan validFor)
    {
        // Secure 6-digit numeric code
        var codeNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var code = codeNumber.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var token = Guid.NewGuid().ToString("N");
        var qrPayload = $"seniorconnect://onboarding/claim?code={code}&token={token}";

        return new Zugangskarte
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            CreatedByCaregiverUserId = createdByCaregiverUserId,
            PairingCode = code,
            QrToken = token,
            QrPayload = qrPayload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(validFor),
            MaxAttempts = 5,
            FailedAttempts = 0
        };
    }

    public void RecordFailedAttempt()
    {
        FailedAttempts++;
    }

    public Result Claim(Guid? claimerUserId = null, string? qrToken = null)
    {
        if (IsClaimed)
            return Error.Conflict("ALREADY_CLAIMED", "This Zugangskarte has already been claimed.");

        if (IsExhausted)
            return Error.Conflict("ZUGANGSKARTE_EXHAUSTED", "This Zugangskarte has been locked due to too many failed attempts.");

        if (ExpiresAtUtc < DateTimeOffset.UtcNow)
            return Error.Conflict("EXPIRED", "This Zugangskarte has expired.");

        if (!string.IsNullOrWhiteSpace(qrToken) && !string.Equals(QrToken, qrToken, StringComparison.Ordinal))
        {
            RecordFailedAttempt();
            return Error.Validation("Invalid QR token provided.");
        }

        ClaimedAtUtc = DateTimeOffset.UtcNow;
        ClaimedByUserId = claimerUserId ?? SeniorUserId;
        return Result.Success();
    }
}
