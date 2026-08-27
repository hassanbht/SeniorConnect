using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public enum VerificationProviderType
{
    Manual,
    Organization,
    IdAustria,
    EID
}

public sealed record IdentityVerificationRequest(
    Guid UserId,
    VerificationProviderType ProviderType,
    string? ExternalTokenReference = null,
    string? OrganizationVouchNotes = null);

public sealed record IdentityVerificationResult(
    bool IsSuccess,
    Guid UserId,
    string ProviderName,
    DateTimeOffset VerifiedAtUtc,
    string VerificationReferenceHash,
    string? FailureReason = null);

public interface IIdentityVerificationProvider
{
    VerificationProviderType ProviderType { get; }
    Task<Result<IdentityVerificationResult>> VerifyIdentityAsync(
        IdentityVerificationRequest request,
        CancellationToken cancellationToken = default);
}
