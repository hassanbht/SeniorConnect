using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class OtpService : IOtpService
{
    private readonly IIdentityDbContext _db;
    private readonly IIdentityHashingService _hashing;
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;

    public OtpService(
        IIdentityDbContext db,
        IIdentityHashingService hashing,
        ISmsSender smsSender,
        IEmailSender emailSender)
    {
        _db = db;
        _hashing = hashing;
        _smsSender = smsSender;
        _emailSender = emailSender;
    }

    public async Task<Result<string>> GenerateAndSendPhoneOtpAsync(
        string phone,
        OtpPurpose purpose,
        string? ipAddress,
        string preferredLocale,
        CancellationToken cancellationToken = default)
    {
        var destinationHash = _hashing.HashDestination(phone);
        var ipHash = _hashing.HashIp(ipAddress);

        // Rate-limiting check: max 3 requests per destination in a 10-minute window
        var tenMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recentRequestsCount = await _db.OtpChallenges
            .CountAsync(c => c.DestinationHash == destinationHash && c.CreatedAtUtc >= tenMinutesAgo, cancellationToken);

        if (recentRequestsCount >= 3)
        {
            return new Error(
                "RATE_LIMITED",
                "Too many verification requests. Please wait a few minutes before trying again.",
                ErrorKind.Validation);
        }

        // Generate 6-digit numeric OTP code
        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var codeHash = _hashing.HashCode(code);

        // Find user if existing
        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Phone == phone && !u.IsDeleted, cancellationToken);

        var challenge = OtpChallenge.Create(
            destinationHash: destinationHash,
            codeHash: codeHash,
            channel: OtpChannel.Sms,
            purpose: purpose,
            userId: existingUser?.Id,
            ipHash: ipHash,
            maxAttempts: 5,
            expiryMinutes: 5);

        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        // Localized SMS text
        var message = preferredLocale switch
        {
            "fa" => $"کد تأیید میتاناند شما: {code} (معتبر تا ۵ دقیقه)",
            "en" => $"Your SeniorConnect verification code is: {code} (valid for 5 minutes)",
            _ => $"Ihr SeniorConnect Bestätigungscode lautet: {code} (5 Minuten gültig)"
        };

        var sendResult = await _smsSender.SendSmsAsync(phone, message, cancellationToken);
        if (sendResult.IsFailure)
        {
            return sendResult.Error!;
        }

        return Result<string>.Success(challenge.Id.ToString());
    }

    public async Task<Result<string>> GenerateAndSendEmailOtpAsync(
        string email,
        OtpPurpose purpose,
        string? ipAddress,
        string preferredLocale,
        CancellationToken cancellationToken = default)
    {
        var destinationHash = _hashing.HashDestination(email);
        var ipHash = _hashing.HashIp(ipAddress);

        // Rate limiting: max 3 requests per destination in 10 minutes
        var tenMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recentRequestsCount = await _db.OtpChallenges
            .CountAsync(c => c.DestinationHash == destinationHash && c.CreatedAtUtc >= tenMinutesAgo, cancellationToken);

        if (recentRequestsCount >= 3)
        {
            return new Error(
                "RATE_LIMITED",
                "Too many verification requests. Please wait a few minutes before trying again.",
                ErrorKind.Validation);
        }

        // Generate 6-digit numeric OTP code
        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var codeHash = _hashing.HashCode(code);

        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);

        var challenge = OtpChallenge.Create(
            destinationHash: destinationHash,
            codeHash: codeHash,
            channel: OtpChannel.Email,
            purpose: purpose,
            userId: existingUser?.Id,
            ipHash: ipHash,
            maxAttempts: 5,
            expiryMinutes: 5);

        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        var (subject, body) = preferredLocale switch
        {
            "fa" => ("کد ورود به میتاناند", $"کد تأیید ورود شما به میتاناند: {code} (اعتبار: ۵ دقیقه)"),
            "en" => ("Your SeniorConnect Login Code", $"Your SeniorConnect verification code is: {code} (valid for 5 minutes)"),
            _ => ("Ihr SeniorConnect Anmeldecode", $"Ihr SeniorConnect Bestätigungscode lautet: {code} (5 Minuten gültig)")
        };

        var sendResult = await _emailSender.SendEmailAsync(email, subject, body, cancellationToken);
        if (sendResult.IsFailure)
        {
            return sendResult.Error!;
        }

        return Result<string>.Success(challenge.Id.ToString());
    }

    public async Task<Result> ValidateAndConsumeOtpAsync(
        string destination,
        string code,
        OtpChannel channel,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var destinationHash = _hashing.HashDestination(destination);
        var codeHash = _hashing.HashCode(code);

        // Fetch latest active challenge for this destination and purpose
        var challenge = await _db.OtpChallenges
            .Where(c => c.DestinationHash == destinationHash
                        && c.Channel == channel
                        && c.Purpose == purpose
                        && c.ConsumedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return new Error(
                "OTP_NOT_FOUND",
                "No active verification code found. Please request a new code.",
                ErrorKind.NotFound);
        }

        if (challenge.IsExpired)
        {
            return new Error(
                "OTP_EXPIRED",
                "The verification code has expired. Please request a new code.",
                ErrorKind.Conflict);
        }

        if (challenge.IsExhausted)
        {
            return new Error(
                "OTP_EXHAUSTED",
                "Too many incorrect attempts. Please request a new code.",
                ErrorKind.Conflict);
        }

        // Record attempt
        challenge.RecordAttempt();

        // Check if code matches
        if (challenge.CodeHash != codeHash)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new Error(
                "INVALID_OTP_CODE",
                $"Invalid code. You have {challenge.MaxAttempts - challenge.Attempts} attempts remaining.",
                ErrorKind.Validation,
                new Dictionary<string, object>
                {
                    ["remainingAttempts"] = challenge.MaxAttempts - challenge.Attempts
                });
        }

        // Consume challenge
        challenge.Consume();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
