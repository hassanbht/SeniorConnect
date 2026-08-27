using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public sealed record VerificationContext(
    string? DocumentReference = null,
    Guid? OrganizationId = null,
    string? Notes = null);

public sealed record VerificationStartResult(
    string ExternalReference,
    bool RequiresExternalRedirect,
    string? RedirectUrl = null);

public sealed record VerificationOutcome(
    bool IsVerified,
    string? RejectionReason = null,
    DateTimeOffset? ValidUntilUtc = null,
    string? IssuerReference = null);

/// <summary>
/// Provider abstraction for identity/trust verifications (BR-TRUST-06, ADR-013).
/// Supports manual, organization, ID Austria and commercial KYC providers.
/// </summary>
public interface IIdentityVerificationProvider
{
    string ProviderKey { get; }
    Task<VerificationStartResult> StartAsync(Guid userId, VerificationType type, VerificationContext ctx, CancellationToken ct = default);
    Task<VerificationOutcome> GetOutcomeAsync(string externalReference, CancellationToken ct = default);
    bool Supports(VerificationType type);
}
