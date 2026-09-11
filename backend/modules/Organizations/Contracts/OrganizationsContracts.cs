namespace SeniorConnect.Modules.Organizations.Contracts;

/// <summary>
/// Cross-module contract for resolving who should be notified as "the
/// coordinator" for an organization — used by Matching's P3-13 escalation
/// path so it never needs a direct reference to the Organizations DbContext.
/// </summary>
public interface IOrganizationCoordinatorReader
{
    Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default);
}
