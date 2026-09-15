using System.Security.Cryptography;
using System.Security.Cryptography;
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
    private readonly IGoogleIdTokenValidator _googleValidator;
    private readonly IIdAustriaClient _idAustriaClient;
    private readonly IEmailSender _emailSender;
    private readonly ITotpService _totpService;
    private readonly ISmsSender _smsSender;
    private readonly IPhotoStorage _photoStorage;

    public IdentityService(
        IIdentityDbContext db,
        IOtpService otpService,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ITrustLevelCalculator trustCalculator,
        ICapabilityService capabilityService,
        IIdentityHashingService hashing,
        IGoogleIdTokenValidator googleValidator,
        IIdAustriaClient idAustriaClient,
        IEmailSender emailSender,
        ITotpService totpService,
        ISmsSender smsSender,
        IPhotoStorage photoStorage)
    {
        _db = db;
        _otpService = otpService;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _trustCalculator = trustCalculator;
        _capabilityService = capabilityService;
        _hashing = hashing;
        _googleValidator = googleValidator;
        _idAustriaClient = idAustriaClient;
        _emailSender = emailSender;
        _totpService = totpService;
        _smsSender = smsSender;
        _photoStorage = photoStorage;
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

        if (user.TotpEnabledAtUtc.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return new Error("TOTP_CODE_REQUIRED", "A two-factor authentication code is required.", ErrorKind.Unauthenticated);
            }

            if (!_totpService.ValidateCode(user.TotpSecret!, request.TotpCode))
            {
                return new Error("TOTP_CODE_INVALID", "The two-factor authentication code is invalid.", ErrorKind.Unauthenticated);
            }
        }

        user.RecordLogin();
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<TotpEnrollmentResponse>> EnrollTotpAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var secret = _totpService.GenerateSecret();
        user.EnrollTotp(secret);
        await _db.SaveChangesAsync(cancellationToken);

        var accountLabel = user.Email ?? user.Id.ToString();
        var provisioningUri = _totpService.BuildProvisioningUri(secret, accountLabel);

        return Result<TotpEnrollmentResponse>.Success(new TotpEnrollmentResponse(secret, provisioningUri));
    }

    public async Task<Result> ConfirmTotpEnrollmentAsync(
        Guid userId,
        ConfirmTotpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Error.Validation("Code is required.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        if (user.TotpSecret is null)
        {
            return new Error("TOTP_NOT_ENROLLED", "No TOTP enrollment is in progress for this account.", ErrorKind.Conflict);
        }

        if (!_totpService.ValidateCode(user.TotpSecret, request.Code))
        {
            return new Error("TOTP_CODE_INVALID", "The two-factor authentication code is invalid.", ErrorKind.Validation);
        }

        user.ConfirmTotpEnrollment();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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

        var oldPhone = user.Phone;
        var email = user.Email;
        var locale = user.PreferredLocale;

        user.ChangePhone(normalizedNewPhone);

        // BR-AUTH-06 (SIM-swap mitigation): invalidate every session on a
        // phone-number change, then notify the OLD number and email so the
        // legitimate owner can react if they didn't request this.
        await _tokenService.RevokeAllSessionsAsync(user.Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldPhone))
        {
            var smsMessage = locale switch
            {
                "fa" => "شماره تلفن حساب میتاناند شما تغییر کرد. اگر این تغییر را شما انجام نداده‌اید، فوراً با پشتیبانی تماس بگیرید.",
                "en" => "Your SeniorConnect account phone number was changed. If you did not make this change, contact support immediately.",
                _ => "Die Telefonnummer Ihres SeniorConnect-Kontos wurde geändert. Falls Sie dies nicht veranlasst haben, kontaktieren Sie umgehend den Support."
            };
            await _smsSender.SendSmsAsync(oldPhone, smsMessage, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var (subject, body) = locale switch
            {
                "fa" => ("تغییر شماره تلفن حساب شما", "شماره تلفن حساب میتاناند شما تغییر کرد. اگر این تغییر را شما انجام نداده‌اید، فوراً با پشتیبانی تماس بگیرید."),
                "en" => ("Your SeniorConnect phone number was changed", "Your account's phone number was changed. If you did not make this change, contact support immediately."),
                _ => ("Ihre SeniorConnect-Telefonnummer wurde geändert", "Die Telefonnummer Ihres Kontos wurde geändert. Falls Sie dies nicht veranlasst haben, kontaktieren Sie umgehend den Support.")
            };
            await _emailSender.SendEmailAsync(email, subject, body, cancellationToken);
        }

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

    // ADR-021: Email + Password registration with verification
    public async Task<Result<string>> RegisterEmailPasswordAsync(
        RegisterEmailPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Error.Validation("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("Password is required.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return new Error("PASSWORDS_DO_NOT_MATCH", "Password and confirmation do not match.", ErrorKind.Validation);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (existingUser is not null)
        {
            return new Error("EMAIL_ALREADY_REGISTERED", "An account with this email already exists.", ErrorKind.Conflict);
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = User.CreateWithEmailPassword(
            email: normalizedEmail,
            displayName: normalizedEmail.Split('@')[0],
            passwordHash: passwordHash,
            preferredLocale: request.PreferredLocale);

        _db.Users.Add(user);

        // Create email verification token (24h expiry)
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = _hashing.HashToken(rawToken);
        var verificationToken = EmailVerificationToken.Create(
            userId: user.Id,
            tokenHash: tokenHash,
            expiresAtUtc: DateTimeOffset.UtcNow.AddHours(24));

        _db.EmailVerificationTokens.Add(verificationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var verificationLink = $"https://seniorconnect.at/verify-email?token={rawToken}";
        var (subject, body) = request.PreferredLocale switch
        {
            "fa" => ("تایید ایمیل شما در میتاناند", $"برای تایید ایمیل خود روی این لینک کلیک کنید: {verificationLink} (اعتبار: ۲۴ ساعت)"),
            "en" => ("Verify your SeniorConnect email", $"Please verify your email by clicking: {verificationLink} (valid 24 hours)"),
            _ => ("Bestätigen Sie Ihre SeniorConnect E-Mail", $"Bitte bestätigen Sie Ihre E-Mail-Adresse: {verificationLink} (24 Stunden gültig)")
        };
        await _emailSender.SendEmailAsync(normalizedEmail, subject, body, cancellationToken);

        return Result<string>.Success(user.Id.ToString());
    }

    public async Task<Result> VerifyEmailRegistrationAsync(
        VerifyEmailRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Error.Validation("Verification token is required.");
        }

        var tokenHash = _hashing.HashToken(request.Token.Trim());

        var token = await _db.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || !token.IsValid)
        {
            return new Error("TOKEN_INVALID_OR_EXPIRED", "The verification token is invalid or has expired.", ErrorKind.Conflict);
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == token.UserId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        user.VerifyEmail();
        token.MarkUsed();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AuthResponse>> EmailPasswordLoginAsync(
        EmailPasswordLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("Email and password are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is null || user.PrimaryAuthMethod != AuthMethod.EmailPassword || user.PasswordHash is null)
        {
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.", ErrorKind.Unauthenticated);
        }

        if (user.Status != UserStatus.Active)
        {
            return new Error("ACCOUNT_SUSPENDED", "Account is not active.", ErrorKind.Forbidden);
        }

        if (!user.EmailVerifiedAtUtc.HasValue)
        {
            return new Error("EMAIL_VERIFICATION_REQUIRED", "Please verify your email address before logging in.", ErrorKind.Forbidden,
                new Dictionary<string, object> { ["emailVerified"] = false });
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return new Error("INVALID_CREDENTIALS", "Invalid email or password.", ErrorKind.Unauthenticated);
        }

        user.RecordLogin();
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Error.Validation("Google ID token is required.");
        }

        var validationResult = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        var payload = validationResult.Value!;
        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();

        // Check if Google account is already linked
        var existingLogin = await _db.UserExternalLogins
            .FirstOrDefaultAsync(l => l.Provider == "google" && l.ProviderKey == payload.Subject, cancellationToken);

        if (existingLogin is not null)
        {
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == existingLogin.UserId && !u.IsDeleted, cancellationToken);

            if (existingUser is not null)
            {
                existingUser.RecordLogin();
                await _db.SaveChangesAsync(cancellationToken);
                return await BuildAuthResponseAsync(existingUser, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
            }
        }

        // Check if email exists
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is not null)
        {
            // Link Google to existing account
            user.LinkExternalLogin(AuthMethod.Google, payload.Subject);
            _db.UserExternalLogins.Add(UserExternalLogin.Create(user.Id, "google", payload.Subject, payload.Email, payload.Name));
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Create new user with Google auth
            user = User.CreateWithGoogle(normalizedEmail, payload.Name ?? normalizedEmail.Split('@')[0], payload.Subject);
            _db.Users.Add(user);
            _db.UserExternalLogins.Add(UserExternalLogin.Create(user.Id, "google", payload.Subject, payload.Email, payload.Name));
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginWithIdAustriaAsync(
        IdAustriaLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Error.Validation("Authorization code is required.");
        }

        var tokenResult = await _idAustriaClient.ExchangeCodeForTokensAsync(request.Code, request.State, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return tokenResult.Error!;
        }

        var tokenResponse = tokenResult.Value!;

        var userInfoResult = await _idAustriaClient.GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
        if (userInfoResult.IsFailure)
        {
            return userInfoResult.Error!;
        }

        var userInfo = userInfoResult.Value!;
        var normalizedEmail = userInfo.Email.Trim().ToLowerInvariant();

        // Check if ID Austria account is already linked
        var existingLogin = await _db.UserExternalLogins
            .FirstOrDefaultAsync(l => l.Provider == "id_austria" && l.ProviderKey == userInfo.Subject, cancellationToken);

        if (existingLogin is not null)
        {
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == existingLogin.UserId && !u.IsDeleted, cancellationToken);

            if (existingUser is not null)
            {
                existingUser.RecordLogin();
                await _db.SaveChangesAsync(cancellationToken);
                return await BuildAuthResponseAsync(existingUser, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
            }
        }

        // Check if email exists
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is not null)
        {
            // Link ID Austria to existing account
            user.LinkExternalLogin(AuthMethod.IdAustria, userInfo.Subject);
            _db.UserExternalLogins.Add(UserExternalLogin.Create(user.Id, "id_austria", userInfo.Subject, userInfo.Email, $"{userInfo.GivenName} {userInfo.FamilyName}".Trim()));
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Create new user with ID Austria auth
            user = User.CreateWithIdAustria(normalizedEmail, $"{userInfo.GivenName} {userInfo.FamilyName}".Trim(), userInfo.Subject);
            _db.Users.Add(user);
            _db.UserExternalLogins.Add(UserExternalLogin.Create(user.Id, "id_austria", userInfo.Subject, userInfo.Email, $"{userInfo.GivenName} {userInfo.FamilyName}".Trim()));
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ID Austria login automatically grants Identity verification -> trust level 1/2 (BR-AUTH-08)
        var identityVerification = await _db.Verifications
            .FirstOrDefaultAsync(v => v.UserId == user.Id && v.Type == VerificationType.Identity && v.Provider == VerificationProvider.IdAustria, cancellationToken);

        if (identityVerification is null)
        {
            identityVerification = Verification.Create(user.Id, VerificationType.Identity, VerificationProvider.IdAustria);
            identityVerification.Verify(user.Id, validUntilUtc: null, verifiedByOrganizationId: null, externalReference: userInfo.Subject);
            _db.Verifications.Add(identityVerification);
        }
        else if (identityVerification.Status != VerificationStatus.Verified)
        {
            identityVerification.Verify(user.Id, validUntilUtc: null, verifiedByOrganizationId: null, externalReference: userInfo.Subject);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, request.DeviceLabel, request.IsPersonalDevice, cancellationToken);
    }

    public async Task<Result<string>> RequestProfilePhoneVerificationAsync(
        Guid userId,
        RequestProfilePhoneVerificationRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return Error.Validation("Phone number is required.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        // Check if phone is already verified for this user
        if (user.PhoneVerifiedAtUtc.HasValue && user.Phone == request.Phone.Trim())
        {
            return Result<string>.Success("Phone already verified");
        }

        return await _otpService.GenerateAndSendPhoneOtpAsync(
            request.Phone.Trim(),
            OtpPurpose.PhoneVerification,
            ipAddress,
            request.PreferredLocale,
            cancellationToken);
    }

    public async Task<Result> VerifyProfilePhoneAsync(
        Guid userId,
        VerifyProfilePhoneRequest request,
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
            OtpPurpose.PhoneVerification,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            return validationResult.Error!;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        user.ChangePhone(normalizedPhone);
        await _db.SaveChangesAsync(cancellationToken);

        // Also record a Phone verification
        var phoneVerification = await _db.Verifications
            .FirstOrDefaultAsync(v => v.UserId == user.Id && v.Type == VerificationType.Phone, cancellationToken);

        if (phoneVerification is null)
        {
            phoneVerification = Verification.Create(user.Id, VerificationType.Phone);
            phoneVerification.Verify(user.Id);
            _db.Verifications.Add(phoneVerification);
        }
        else if (phoneVerification.Status != VerificationStatus.Verified)
        {
            phoneVerification.Verify(user.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<UserSummaryDto>> UploadProfilePhotoAsync(
        Guid userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var storageResult = await _photoStorage.SaveProfilePhotoAsync(userId, content, contentType, cancellationToken);
        if (storageResult.IsFailure)
        {
            return storageResult.Error!;
        }

        user.SetPhoto(storageResult.Value);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserSummaryDto>.Success(MapUserSummary(user));
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
        Status: user.Status.ToString(),
        PhotoUrl: user.PhotoUrl);
}
