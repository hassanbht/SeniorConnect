using System.Security.Cryptography;
using System.Text;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

/// <summary>
/// Austrian eID / ID Austria verification provider (P8-06, BR-TRUST-06).
/// Complies strictly with BR-TRUST-05: stores only the verified outcome and reference hash,
/// and never stores identity cards, passports, or citizen card certificates.
/// </summary>
public sealed class IdAustriaVerificationProvider : IIdentityVerificationProvider
{
    public VerificationProviderType ProviderType => VerificationProviderType.IdAustria;

    public Task<Result<IdentityVerificationResult>> VerifyIdentityAsync(
        IdentityVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
        {
            return Task.FromResult<Result<IdentityVerificationResult>>(Error.Validation("UserId is required for identity verification."));
        }

        // Mock/Sandbox validation of ID Austria signed token claim
        var reference = request.ExternalTokenReference ?? $"IDAUST_{request.UserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference)));

        var result = new IdentityVerificationResult(
            IsSuccess: true,
            UserId: request.UserId,
            ProviderName: "ID Austria (BRZ / BMI eID)",
            VerifiedAtUtc: DateTimeOffset.UtcNow,
            VerificationReferenceHash: hash,
            FailureReason: null);

        return Task.FromResult(Result<IdentityVerificationResult>.Success(result));
    }
}
