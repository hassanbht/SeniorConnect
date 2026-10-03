using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Identity.Tests;

/// <summary>
/// Deployment-integration phase (§4): security tests for the email/OTP
/// provider boundary. Pins the production-honest behavior introduced with the
/// SmtpEmailSender configuration path:
///  - no fake production provider exists; unconfigured sends fail honestly;
///  - OTP codes and provider credentials never reach logs;
///  - expired / invalid / replayed OTPs are denied;
///  - a transient provider failure never destroys an existing session.
/// </summary>
public sealed class EmailOtpProviderSecurityTests
{
    private const string MagicLinkEmail = "magic-link-test@seniorconnect.example";

    /// <summary>Builds a service stack on InMemory EF with a log-capturing email adapter.</summary>
    private static (IdentityService Service, SeniorConnectDbContext Db, IdentityHashingService Hashing, RecordingLogger Logger)
        BuildStack(bool isDevelopment)
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeniorConnect_EmailOtp_{Guid.NewGuid()}")
            .Options;
        var db = new SeniorConnectDbContext(options, new DefaultTenantContext());

        var hashing = new IdentityHashingService();
        var logger = new RecordingLogger();
        var emailSender = new SmtpEmailSender(logger, new EmailProviderOptions(), isDevelopment);
        var smsSender = new SmsSender(NullLogger<SmsSender>.Instance, logMessagesForDevelopment: false);

        var tokenService = new JwtTokenService(db, hashing, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var otpService = new OtpService(db, hashing, smsSender, emailSender);
        var service = new IdentityService(
            db,
            otpService,
            tokenService,
            new PasswordHasher(),
            new TrustLevelCalculator(),
            new CapabilityService(db),
            hashing,
            new GoogleIdTokenValidatorStub(NullLogger<GoogleIdTokenValidatorStub>.Instance),
            new IdAustriaClientStub(NullLogger<IdAustriaClientStub>.Instance),
            emailSender,
            new TotpService(),
            smsSender,
            new LocalDiskPhotoStorage(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:PhotoUploadPath"] = Path.Combine(Path.GetTempPath(), "sc_email_otp_" + Guid.NewGuid()) })
                .Build()));

        return (service, db, hashing, logger);
    }

    private static string? FindCodeByHash(IdentityHashingService hashing, string codeHash)
    {
        for (var i = 100_000; i < 1_000_000; i++)
        {
            var candidate = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (hashing.HashCode(candidate) == codeHash)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Expiry is part of the persisted challenge; tests move it via the property's backing setter.</summary>
    private static void Expire(OtpChallenge challenge)
    {
        typeof(OtpChallenge).GetProperty(nameof(OtpChallenge.ExpiresAtUtc))!
            .SetValue(challenge, DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    // --- Production-honest failure behavior ---------------------------------

    [Fact]
    public async Task EmailOtp_WithoutConfiguredProvider_FailsHonestly_InProduction()
    {
        var (service, db, _, logger) = BuildStack(isDevelopment: false);
        try
        {
            var result = await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            Assert.True(result.IsFailure);
            Assert.Equal("EMAIL_PROVIDER_NOT_CONFIGURED", result.Error!.Code);
            Assert.Contains("No email provider configured", logger.SingleRecord, StringComparison.Ordinal);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task EmailOtp_InDevelopment_UsesLogFallback_WithoutProvider()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: true);
        try
        {
            var result = await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            Assert.True(result.IsSuccess);
            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            Assert.NotNull(FindCodeByHash(hashing, challenge.CodeHash));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public void IncompleteSmtpSelection_FailsFastAtStartup()
    {
        var options = new EmailProviderOptions { Provider = "smtp", Host = "relay.example", SenderAddress = null };

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => options.EnsureValidSelection());
    }

    [Fact]
    public void CompleteSmtpSelection_IsAccepted()
    {
        var options = new EmailProviderOptions { Provider = "smtp", Host = "relay.example", SenderAddress = "no-reply@seniorconnect.example" };

        options.EnsureValidSelection();

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void EnvironmentVariables_OverrideConfiguredCredentials_SoSecretsStayInTheSecretStore()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "smtp",
                ["Email:Host"] = "relay.example",
                ["Email:SenderAddress"] = "no-reply@seniorconnect.example",
                ["Email:Username"] = "committed-value-that-must-lose",
            })
            .Build();
        Environment.SetEnvironmentVariable("EMAIL_USERNAME", "secret-store-value");
        try
        {
            var options = EmailProviderOptions.Resolve(configuration);

            Assert.Equal("secret-store-value", options.Username);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EMAIL_USERNAME", null);
        }
    }

    // --- OTP / credential log hygiene ----------------------------------------

    [Fact]
    public async Task EmailOtp_NeverLogsTheCode_WhenSendFailsInProduction()
    {
        var (service, db, hashing, logger) = BuildStack(isDevelopment: false);
        try
        {
            var requestResult = await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");
            Assert.True(requestResult.IsFailure); // unconfigured production provider

            var codeHash = (await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email)).CodeHash;
            var code = FindCodeByHash(hashing, codeHash);
            Assert.NotNull(code);

            Assert.All(logger.Records, record => Assert.DoesNotContain(code, record));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task EmailOtp_LogMessages_NeverCarryCredentials()
    {
        var (service, db, _, logger) = BuildStack(isDevelopment: false);
        try
        {
            await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            Assert.All(logger.Records, record =>
            {
                Assert.DoesNotContain("EMAIL_PASSWORD", record);
                Assert.DoesNotContain("EMAIL_USERNAME", record);
                Assert.DoesNotContain("Email:Password", record);
            });
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task DevelopmentLogMode_NeverLogsThePlaintextDestination()
    {
        var (service, db, hashing, logger) = BuildStack(isDevelopment: true);
        try
        {
            var requestResult = await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");
            Assert.True(requestResult.IsSuccess);

            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            var code = FindCodeByHash(hashing, challenge.CodeHash);
            Assert.NotNull(code);

            // The dev log carries the message so developers can sign in; the
            // plaintext destination must still never appear (masking only).
            Assert.All(logger.Records, record =>
            {
                Assert.DoesNotContain(MagicLinkEmail, record);
                Assert.DoesNotContain("verify-email?token=", record);
            });
        }
        finally
        {
            db.Dispose();
        }
    }

    // --- OTP validation semantics --------------------------------------------

    [Fact]
    public async Task EmailOtp_Expired_IsDenied()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: true);
        try
        {
            await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            var code = FindCodeByHash(hashing, challenge.CodeHash)!;
            Expire(challenge);

            var verify = await service.VerifyEmailMagicLinkAsync(
                new VerifyEmailMagicLinkRequest(MagicLinkEmail, code, "Test Device", IsPersonalDevice: true));

            Assert.True(verify.IsFailure);
            Assert.Equal("OTP_EXPIRED", verify.Error!.Code);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task EmailOtp_Invalid_IsDenied_AndConsumesAnAttempt()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: true);
        try
        {
            await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            var wrong = challenge.CodeHash == hashing.HashCode("000000") ? "000001" : "000000";

            var verify = await service.VerifyEmailMagicLinkAsync(
                new VerifyEmailMagicLinkRequest(MagicLinkEmail, wrong, "Test Device", IsPersonalDevice: true));

            Assert.True(verify.IsFailure);
            Assert.Equal("INVALID_OTP_CODE", verify.Error!.Code);
            var reloaded = await db.OtpChallenges.SingleAsync(c => c.Id == challenge.Id);
            Assert.Equal(1, (int)reloaded.Attempts);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task EmailOtp_Replay_IsDenied()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: true);
        try
        {
            await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            var code = FindCodeByHash(hashing, challenge.CodeHash)!;

            var first = await service.VerifyEmailMagicLinkAsync(
                new VerifyEmailMagicLinkRequest(MagicLinkEmail, code, "Test Device", IsPersonalDevice: true));
            Assert.True(first.IsSuccess);

            var replay = await service.VerifyEmailMagicLinkAsync(
                new VerifyEmailMagicLinkRequest(MagicLinkEmail, code, "Test Device", IsPersonalDevice: true));

            Assert.True(replay.IsFailure);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task EmailOtp_ExhaustedAttempts_AreDenied_EvenWithTheCorrectCode()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: true);
        try
        {
            await service.RequestEmailMagicLinkAsync(
                new RequestEmailMagicLinkRequest(MagicLinkEmail, PreferredLocale: "en"),
                ipAddress: "127.0.0.1");

            var challenge = await db.OtpChallenges.SingleAsync(c => c.Channel == OtpChannel.Email);
            for (var attempt = 0; attempt < challenge.MaxAttempts; attempt++)
            {
                await service.VerifyEmailMagicLinkAsync(
                    new VerifyEmailMagicLinkRequest(MagicLinkEmail, "000000", "Test Device", IsPersonalDevice: true));
            }

            var correct = FindCodeByHash(hashing, challenge.CodeHash)!;
            var afterExhaustion = await service.VerifyEmailMagicLinkAsync(
                new VerifyEmailMagicLinkRequest(MagicLinkEmail, correct, "Test Device", IsPersonalDevice: true));

            Assert.True(afterExhaustion.IsFailure);
            Assert.Equal("OTP_EXHAUSTED", afterExhaustion.Error!.Code);
        }
        finally
        {
            db.Dispose();
        }
    }

    // --- Registration-token replay (via the development log fallback) ---------

    [Fact]
    public async Task EmailVerificationToken_Replay_IsDenied()
    {
        var (service, db, _, logger) = BuildStack(isDevelopment: true);
        try
        {
            var register = await service.RegisterEmailPasswordAsync(
                new RegisterEmailPasswordRequest("verify-token-test@seniorconnect.example", "S3curePassword!", "S3curePassword!", "en"),
                ipAddress: "127.0.0.1");
            Assert.True(register.IsSuccess);

            // The development log fallback carries the verification link —
            // recover the raw token from it the way a developer would.
            var match = Regex.Match(string.Join("\n", logger.Records), @"verify-email\?token=([0-9a-f]{64})");
            Assert.True(match.Success, "development email log did not contain the verification link");
            var rawToken = match.Groups[1].Value;

            var first = await service.VerifyEmailRegistrationAsync(new VerifyEmailRegistrationRequest(rawToken));
            Assert.True(first.IsSuccess);

            var replay = await service.VerifyEmailRegistrationAsync(new VerifyEmailRegistrationRequest(rawToken));
            Assert.True(replay.IsFailure);
            Assert.Equal("TOKEN_INVALID_OR_EXPIRED", replay.Error!.Code);
        }
        finally
        {
            db.Dispose();
        }
    }

    // --- Session safety on provider failure ------------------------------------

    [Fact]
    public async Task TransientProviderFailure_DuringPhoneChange_DoesNotRevokeExistingSessions()
    {
        var (service, db, hashing, _) = BuildStack(isDevelopment: false);
        try
        {
            const string oldPhone = "+436603210000";
            var user = User.CreateWithPhone(oldPhone, "Session Safety");
            user.VerifyPhone();
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var tokenService = new JwtTokenService(db, hashing, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            var (rawToken, _) = (await tokenService.CreateRefreshTokenAsync(user.Id, "Existing Device", true)).Value!;

            var change = await service.InitiatePhoneChangeAsync(
                user.Id, new InitiatePhoneChangeRequest("+436603210001"), ipAddress: "127.0.0.1");

            // The change request fails honestly (unconfigured provider)…
            Assert.True(change.IsFailure);
            Assert.Equal("SMS_PROVIDER_NOT_CONFIGURED", change.Error!.Code);

            // …and the pre-existing session must survive: refresh still works.
            var refresh = await service.RefreshTokenAsync(new RefreshTokenRequest(rawToken));
            Assert.True(refresh.IsSuccess);
        }
        finally
        {
            db.Dispose();
        }
    }

    /// <summary>ILogger that records every formatted message for assertions.</summary>
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger<SmtpEmailSender>
    {
        private readonly List<string> _records = [];

        public IReadOnlyList<string> Records => _records;

        public string SingleRecord => _records.Count == 1
            ? _records[0]
            : throw new InvalidOperationException($"Expected exactly one log record, found {_records.Count}.");

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _records.Add(formatter(state, exception));
        }
    }
}
