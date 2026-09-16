using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Contracts;

/// <summary>
/// P6-03: Cross-module contract allowing Family provisioning to create a SupportProfile
/// for the provisioned senior.
/// </summary>
public interface ISupportProfileProvisioner
{
    Task<Result> ProvisionSupportProfileAsync(
        Guid userId,
        string? postalCode,
        string? city,
        Guid? createdByUserId,
        CancellationToken ct = default);
}
