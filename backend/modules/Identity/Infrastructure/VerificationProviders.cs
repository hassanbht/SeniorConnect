using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class ManualVerificationProvider : IIdentityVerificationProvider
{
    public string ProviderKey => "Manual";

    public Task<VerificationStartResult> StartAsync(
        Guid userId,
        VerificationType type,
        VerificationContext ctx,
        CancellationToken ct = default)
    {
        var reference = $"MAN-{userId:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return Task.FromResult(new VerificationStartResult(
            ExternalReference: reference,
            RequiresExternalRedirect: false,
            RedirectUrl: null));
    }

    public Task<VerificationOutcome> GetOutcomeAsync(
        string externalReference,
        CancellationToken ct = default)
    {
        // Manual verifications are completed directly by coordinator action
        return Task.FromResult(new VerificationOutcome(
            IsVerified: true,
            ValidUntilUtc: DateTimeOffset.UtcNow.AddYears(1),
            IssuerReference: externalReference));
    }

    public bool Supports(VerificationType type) =>
        type is VerificationType.Identity
            or VerificationType.Address
            or VerificationType.Training
            or VerificationType.BackgroundCheck
            or VerificationType.Phone
            or VerificationType.Email;
}

public sealed class OrganizationVerificationProvider : IIdentityVerificationProvider
{
    public string ProviderKey => "Organization";

    public Task<VerificationStartResult> StartAsync(
        Guid userId,
        VerificationType type,
        VerificationContext ctx,
        CancellationToken ct = default)
    {
        var orgRef = ctx.OrganizationId.HasValue
            ? $"ORG-{ctx.OrganizationId.Value:N}-{userId:N}"
            : $"ORG-GENERAL-{userId:N}";

        return Task.FromResult(new VerificationStartResult(
            ExternalReference: orgRef,
            RequiresExternalRedirect: false,
            RedirectUrl: null));
    }

    public Task<VerificationOutcome> GetOutcomeAsync(
        string externalReference,
        CancellationToken ct = default)
    {
        return Task.FromResult(new VerificationOutcome(
            IsVerified: true,
            ValidUntilUtc: DateTimeOffset.UtcNow.AddMonths(6),
            IssuerReference: externalReference));
    }

    public bool Supports(VerificationType type) =>
        type is VerificationType.Organization
            or VerificationType.Training
            or VerificationType.BackgroundCheck;
}
