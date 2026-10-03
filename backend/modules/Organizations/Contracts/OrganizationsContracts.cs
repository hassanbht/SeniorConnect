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

    /// <summary>
    /// BR-SG-02: organizations where the user holds an active
    /// SafeguardingOfficer membership. Coordinator/Admin roles never count —
    /// ordinary administrators must not see safeguarding cases.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSafeguardingOfficerOrganizationIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// S-4 (platform officer decision, 2026-09-30): ids of users holding the
    /// explicit PLATFORM-level PlatformSafeguardingOfficer role — an
    /// organization membership with OrganizationId == null. Never implied by
    /// OrganizationAdmin or by an ordinary organization SafeguardingOfficer
    /// membership.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetPlatformSafeguardingOfficerUserIdsAsync(CancellationToken ct = default);
}

public sealed record StaffOrganizationDto(Guid OrganizationId, string OrganizationName);
