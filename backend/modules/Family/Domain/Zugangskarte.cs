using System.Security.Cryptography;
using System.Text;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Family.Domain;

public sealed class Zugangskarte
{
    public const short DefaultMaxAttempts = 5;

    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public Guid CreatedByCaregiverUserId { get; private set; }
    public string PairingCode { get; private set; } = string.Empty; // 6-digit numeric code
    public string QrToken { get; private set; } = string.Empty; // 128-bit random, the non-guessable card identifier
    public string QrPayload { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ClaimedAtUtc { get; private set; }
    public Guid? ClaimedByUserId { get; private set; }
    public short FailedAttempts { get; private set; }
    public short MaxAttempts { get; private set; } = DefaultMaxAttempts;

    public bool IsClaimed => ClaimedAtUtc.HasValue;
    public bool IsExhausted => FailedAttempts >= MaxAttempts;

    private Zugangskarte() { }

    public static Zugangskarte Generate(Guid seniorUserId, Guid createdByCaregiverUserId, TimeSpan validFor)
    {
        var codeNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var code = codeNumber.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var token = RandomNumberGenerator.GetHexString(32, lowercase: true);
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
            MaxAttempts = DefaultMaxAttempts,
            FailedAttempts = 0
        };
    }

    /// <summary>
    /// P6-04: Redeems the card. The card has already been located by its
    /// non-guessable QR token; the 6-digit code is the second factor. Every
    /// wrong code counts against this card and locks it after <see cref="MaxAttempts"/>.
    /// </summary>
    public Result Redeem(string pairingCode, Guid? claimerUserId = null)
    {
        var stateCheck = EnsureClaimable();
        if (stateCheck.IsFailure)
            return stateCheck;

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(PairingCode),
            Encoding.UTF8.GetBytes(pairingCode ?? string.Empty));

        if (!matches)
        {
            FailedAttempts++;
            return IsExhausted
                ? Error.Conflict("ZUGANGSKARTE_EXHAUSTED", "This Zugangskarte has been locked due to too many failed attempts. Please ask for a new card.")
                : new Error(
                    "INVALID_PAIRING_CODE",
                    "The code does not match this Zugangskarte.",
                    ErrorKind.Validation,
                    new Dictionary<string, object> { ["remainingAttempts"] = MaxAttempts - FailedAttempts });
        }

        return Claim(claimerUserId);
    }

    /// <summary>State transition only — callers outside the domain must use <see cref="Redeem"/>.</summary>
    public Result Claim(Guid? claimerUserId = null)
    {
        var stateCheck = EnsureClaimable();
        if (stateCheck.IsFailure)
            return stateCheck;

        ClaimedAtUtc = DateTimeOffset.UtcNow;
        ClaimedByUserId = claimerUserId ?? SeniorUserId;
        return Result.Success();
    }

    private Result EnsureClaimable()
    {
        if (IsClaimed)
            return Error.Conflict("ALREADY_CLAIMED", "This Zugangskarte has already been claimed.");

        if (IsExhausted)
            return Error.Conflict("ZUGANGSKARTE_EXHAUSTED", "This Zugangskarte has been locked due to too many failed attempts. Please ask for a new card.");

        if (ExpiresAtUtc < DateTimeOffset.UtcNow)
            return Error.Conflict("EXPIRED", "This Zugangskarte has expired.");

        return Result.Success();
    }
}
