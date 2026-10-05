using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Contracts;

/// <summary>
/// ADR-029: answers whether an account may manage staff two-factor security.
///
/// Authentication ("how did they sign in") is not authorization ("what may
/// they do"). Password is legitimate for seniors and ordinary volunteers, so
/// staff-ness is derived from active Coordinator/Admin membership — never
/// from the authentication method, TOTP presence, or any client flag.
/// Implemented by the Organizations module; consumed by Identity as an
/// optional dependency that fails closed when absent.
/// </summary>
public interface IStaffSecurityAuthorizer
{
    /// <summary>
    /// True when the user holds an active Coordinator or Admin membership in
    /// any organization. Every other case — unknown user, inactive
    /// membership, ordinary member, volunteer, senior — resolves to false.
    /// </summary>
    Task<bool> CanManageStaffSecurityAsync(Guid userId, CancellationToken ct = default);
}