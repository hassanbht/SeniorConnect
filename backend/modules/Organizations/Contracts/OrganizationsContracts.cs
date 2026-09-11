namespace SeniorConnect.Modules.Organizations.Contracts;

/// <summary>
/// Cross-module contract for resolving who should be notified as "the
/// coordinator" for an organization — used by Matching's P3-13 escalation
/// path so it never needs a direct reference to the Organizations DbContext.
/// </summary>
public interface IOrganizationCoordinatorReader
{
    Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The inverse query: which organizations does this user actively staff
    /// as Coordinator or Admin. Used by Community's P2-25 org page to gate
    /// the "manage this org" entry point and its publish/edit/cancel calls.
    /// </summary>
    Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record StaffOrganizationDto(Guid OrganizationId, string OrganizationName);
