using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class IdentityService : IIdentityService
{
    private readonly IIdentityDbContext _db;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITrustLevelCalculator _trustCalculator;
    private readonly ICapabilityService _capabilityService;
    private readonly IIdentityHashingService _hashing;

    public IdentityService(
        IIdentityDbContext db,
        IOtpService otpService,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ITrustLevelCalculator trustCalculator,
        ICapabilityService capabilityService,
        IIdentityHashingService hashing)
    {
        _db = db;
        _otpService = otpService;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _trustCalculator = trustCalculator;
        _capabilityService = capabilityService;
        _hashing = hashing;
    }

    public async Task<Result<string>> RequestPhoneOtpAsync(
        RequestPhoneOtpRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return Error.Validation("Phone number is required.");
        }

        return await _otpService.GenerateAndSendPhoneOtpAsync(
            request.Phone.Trim(),
            request.Purpose,
            ipAddress,
            request.PreferredLocale,
            cancellationToken);
    }

    public async Task<Result<AuthResponse>> VerifyPhoneOtpAsync(
        VerifyPhoneOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Error.Validation("Phone number and code are required.");
        }

        var normalizedPhone = request.Phone.Trim();

        var validationResult = await _otpService.ValidateAndConsumeOtpAsync(
            normalizedPhone,
            request.Code.Trim(),
            OtpChannel.Sms,
            request.Purpose,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        // Find or create user
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            // Auto-registration on first phone verification
            user = User.CreateWithPhone(
                phone: normalizedPhone,
                displayName: "Volunteer / Senior",
                preferredLocale: "de",
                seniorModeDefault: false);

            user.VerifyPhone();
            _db.Users.Add(user);
        }
        else
        {
            user.VerifyPhone();
            user.RecordLogin();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<string>> RequestEmailMagicLinkAsync(
        RequestEmailMagicLinkRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Error.Validation("Email is required.");
        }

        return await _otpService.GenerateAndSendEmailOtpAsync(
            request.Email.Trim().ToLowerInvariant(),
            request.Purpose,
            ipAddress,
            request.PreferredLocale,
            cancellationToken);
    }

    public async Task<Result<AuthResponse>> VerifyEmailMagicLinkAsync(
        VerifyEmailMagicLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Error.Validation("Email and verification code are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var validationResult = await _otpService.ValidateAndConsumeOtpAsync(
            normalizedEmail,
            request.Code.Trim(),
            OtpChannel.Email,
            request.Purpose,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithEmail(
                email: normalizedEmail,
                displayName: normalizedEmail.Split('@')[0],
                preferredLocale: "de");

            user.VerifyEmail();
            _db.Users.Add(user);
        }
        else
        {
            user.VerifyEmail();
            user.RecordLogin();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<AuthResponse>> StaffLoginAsync(
        StaffLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("Email and password are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is null || user.PrimaryAuthMethod != AuthMethod.Password || user.PasswordHash is null)
        {
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.", ErrorKind.Unauthenticated);
        }

        if (user.Status != UserStatus.Active)
        {
            return new Error("ACCOUNT_SUSPENDED", "Account is not active.", ErrorKind.Forbidden);
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.", ErrorKind.Unauthenticated);
        }

        user.RecordLogin();
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Error.Validation("Refresh token is required.");
        }

        var rotateResult = await _tokenService.RotateRefreshTokenAsync(
            request.RefreshToken,
            request.DeviceLabel,
            cancellationToken);

        if (rotateResult.IsFailure)
        {
            return rotateResult.Error!;
        }

        var (newRawToken, newRefreshTokenEntity, user) = rotateResult.Value;

        var verifications = await _db.Verifications
            .Where(v => v.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var trustEval = _trustCalculator.Evaluate(user, verifications);
        var capabilities = await _capabilityService.ResolveCapabilitiesAsync(user, trustEval.Level, cancellationToken);
        var accessToken = _tokenService.GenerateAccessToken(user, capabilities, trustEval.Level);

        return Result<AuthResponse>.Success(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: newRawToken,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            User: MapUserSummary(user),
            Capabilities: capabilities,
            TrustLevel: trustEval.Level));
    }

    public async Task<Result> LogoutAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result.Success();
        }

        return await _tokenService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);
    }

    public async Task<Result<UserSummaryDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        return Result<UserSummaryDto>.Success(MapUserSummary(user));
    }

    public async Task<Result<UserSummaryDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        user.UpdateProfile(
            displayName: request.DisplayName,
            dateOfBirth: request.DateOfBirth,
            preferredLocale: request.PreferredLocale,
            seniorModeDefault: request.SeniorModeDefault);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserSummaryDto>.Success(MapUserSummary(user));
    }

    public async Task<Result<TrustLevelDto>> GetTrustLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var verifications = await _db.Verifications
            .Where(v => v.UserId == userId)
            .ToListAsync(cancellationToken);

        var evaluation = _trustCalculator.Evaluate(user, verifications);

        var verificationDtos = verifications.Select(v => new VerificationSummaryDto(
            Type: v.Type.ToString(),
            Status: v.Status.ToString(),
            VerifiedAtUtc: v.VerifiedAtUtc,
            ValidUntilUtc: v.ValidUntilUtc,
            RejectionReason: v.RejectionReason)).ToList();

        return Result<TrustLevelDto>.Success(new TrustLevelDto(
            Level: evaluation.Level,
            Verifications: verificationDtos,
            MissingForNextLevel: evaluation.MissingForNextLevel));
    }

    public async Task<Result<IReadOnlyList<string>>> GetCapabilitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var verifications = await _db.Verifications
            .Where(v => v.UserId == userId)
            .ToListAsync(cancellationToken);

        var evaluation = _trustCalculator.Evaluate(user, verifications);
        var capabilities = await _capabilityService.ResolveCapabilitiesAsync(user, evaluation.Level, cancellationToken);

        return Result<IReadOnlyList<string>>.Success(capabilities);
    }

    public async Task<Result<IReadOnlyList<DeviceSessionDto>>> GetActiveDevicesAsync(
        Guid userId,
        string? currentRawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _tokenService.GetActiveDevicesAsync(userId, currentRawRefreshToken, cancellationToken);
        return Result<IReadOnlyList<DeviceSessionDto>>.Success(sessions);
    }

    public async Task<Result> RevokeDeviceSessionAsync(
        Guid userId,
        Guid refreshTokenId,
        CancellationToken cancellationToken = default)
    {
        return await _tokenService.RevokeDeviceSessionAsync(userId, refreshTokenId, cancellationToken);
    }

    public async Task<Result<string>> InitiatePhoneChangeAsync(
        Guid userId,
        InitiatePhoneChangeRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPhone))
        {
            return Error.Validation("New phone number is required.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        // Send OTP to new phone number with purpose PhoneChange
        return await _otpService.GenerateAndSendPhoneOtpAsync(
            request.NewPhone.Trim(),
            OtpPurpose.PhoneChange,
            ipAddress,
            user.PreferredLocale,
            cancellationToken);
    }

    public async Task<Result> VerifyPhoneChangeAsync(
        Guid userId,
        VerifyPhoneChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPhone) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Error.Validation("New phone number and verification code are required.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var normalizedNewPhone = request.NewPhone.Trim();

        var validationResult = await _otpService.ValidateAndConsumeOtpAsync(
            normalizedNewPhone,
            request.Code.Trim(),
            OtpChannel.Sms,
            OtpPurpose.PhoneChange,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        user.ChangePhone(normalizedNewPhone);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RecordConsentAsync(
        Guid userId,
        RecordConsentRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var ipHash = _hashing.HashIp(ipAddress);

        var consent = Consent.Create(
            userId: userId,
            consentType: request.ConsentType,
            documentVersion: request.DocumentVersion,
            granted: request.Granted,
            ipHash: ipHash);

        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<AuthResponse>> BuildAuthResponseAsync(
        User user,
        string? deviceLabel,
        bool isPersonalDevice,
        CancellationToken cancellationToken)
    {
        var verifications = await _db.Verifications
            .Where(v => v.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var trustEval = _trustCalculator.Evaluate(user, verifications);

        // Record TrustLevelSnapshot
        var snapshot = TrustLevelSnapshot.Create(user.Id, trustEval.Level, trustEval.ReasonJson);
        _db.TrustLevelSnapshots.Add(snapshot);

        var capabilities = await _capabilityService.ResolveCapabilitiesAsync(user, trustEval.Level, cancellationToken);
        var accessToken = _tokenService.GenerateAccessToken(user, capabilities, trustEval.Level);

        var refreshResult = await _tokenService.CreateRefreshTokenAsync(
            user.Id,
            deviceLabel,
            isPersonalDevice,
            cancellationToken);

        if (refreshResult.IsFailure)
        {
            return refreshResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshResult.Value.RawToken,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            User: MapUserSummary(user),
            Capabilities: capabilities,
            TrustLevel: trustEval.Level));
    }

    private static UserSummaryDto MapUserSummary(User user) => new(
        Id: user.Id,
        Phone: user.Phone,
        Email: user.Email,
        DisplayName: user.DisplayName,
        PreferredLocale: user.PreferredLocale,
        SeniorModeDefault: user.SeniorModeDefault,
        PrimaryAuthMethod: user.PrimaryAuthMethod.ToString(),
        Status: user.Status.ToString());
}
