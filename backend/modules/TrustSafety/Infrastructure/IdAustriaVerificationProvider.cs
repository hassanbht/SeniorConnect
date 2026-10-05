using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

/// <summary>
/// Governs the external identity-verification provider (BR-TRUST-06).
///
/// The provider is OFF unless an operator explicitly enables it. That default
/// matters: a verification provider that cannot actually reach the authority
/// must never report "verified", because a false Identity verification is the
/// input to every home-visit safety gate.
/// </summary>
public sealed record IdAustriaOptions
{
    public const string SectionName = "IdAustria";

    /// <summary>
    /// Master switch. While false the endpoint answers a stable, documented
    /// refusal and no verification outcome is ever produced.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Only Development may run without a real upstream. A non-Development host
    /// with Enabled=true and no shared secret would still be asserting success
    /// without evidence, so startup refuses it.
    /// </summary>
    public string? SharedSecret { get; set; }

    public static IdAustriaOptions Resolve(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(SectionName);
        return new IdAustriaOptions
        {
            Enabled = section?["Enabled"] is string raw && bool.TryParse(raw, out var parsed) && parsed,
            SharedSecret = section?["SharedSecret"],
        };
    }

    public void EnsureSafeFor(bool isDevelopment)
    {
        if (!Enabled)
        {
            return; // Disabled is always safe: it refuses every request.
        }

        if (string.IsNullOrWhiteSpace(SharedSecret))
        {
            throw new InvalidOperationException(
                $"{SectionName}:SharedSecret must be set when {SectionName}:Enabled is true.");
        }

        if (!isDevelopment && SharedSecret.Length < 32)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SharedSecret must be at least 32 characters outside Development.");
        }
    }
}

/// <summary>
/// Austrian eID / ID Austria verification provider (P8-06, BR-TRUST-06).
/// Complies strictly with BR-TRUST-05: stores only the verified outcome and reference hash,
/// and never stores identity cards, passports, or citizen card certificates.
///
/// <para><b>Fail-closed.</b> This provider performs no cryptographic validation of
/// an upstream assertion, so it MUST NOT report success outside Development.
/// Without that gate it would hand every caller a trusted Identity verification
/// from nothing more than a client-supplied token reference — the exact defect
/// BR-TRUST-06 exists to prevent.</para>
/// </summary>
public sealed class IdAustriaVerificationProvider : IIdentityVerificationProvider
{
    public const string RefusalCode = "IDENTITY_PROVIDER_NOT_CONFIGURED";

    private readonly IdAustriaOptions _options;
    private readonly bool _isDevelopment;

    public IdAustriaVerificationProvider(IdAustriaOptions options, bool isDevelopment)
    {
        _options = options;
        _isDevelopment = isDevelopment;
    }

    public VerificationProviderType ProviderType => VerificationProviderType.IdAustria;

    public Task<Result<IdentityVerificationResult>> VerifyIdentityAsync(
        IdentityVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
        {
            return Task.FromResult<Result<IdentityVerificationResult>>(
                Error.Validation("UserId is required for identity verification."));
        }

        if (!_options.Enabled)
        {
            return Refuse();
        }

        // Outside Development an operator may have switched the provider on, but
        // until real upstream validation exists this code path would still be a
        // rubber stamp. Refuse rather than assert an unverified identity.
        if (!_isDevelopment)
        {
            return Refuse();
        }

        // Mock/Sandbox validation of ID Austria signed token claim
        var reference = request.ExternalTokenReference ?? $"IDAUST_{request.UserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference)));

        var result = new IdentityVerificationResult(
            IsSuccess: true,
            UserId: request.UserId,
            ProviderName: "ID Austria (BRZ / BMI eID) [sandbox]",
            VerifiedAtUtc: DateTimeOffset.UtcNow,
            VerificationReferenceHash: hash,
            FailureReason: null);

        return Task.FromResult(Result<IdentityVerificationResult>.Success(result));
    }

    private static Task<Result<IdentityVerificationResult>> Refuse()
    {
        return Task.FromResult<Result<IdentityVerificationResult>>(
            new Error(
                RefusalCode,
                "Identity verification is not available. An organization coordinator must record the verification instead.",
                ErrorKind.Forbidden));
    }
}
