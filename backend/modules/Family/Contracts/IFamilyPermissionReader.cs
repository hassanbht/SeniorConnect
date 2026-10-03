namespace SeniorConnect.Modules.Family.Contracts;

/// <summary>
/// P6-05 / BR-HELP-04: Cross-module read contract allowing HelpRequests module
/// to verify whether a caregiver is authorized to create requests on behalf of a senior.
/// </summary>
public interface IFamilyPermissionReader
{
    Task<bool> HasPermissionAsync(
        Guid caregiverUserId,
        Guid seniorUserId,
        string permissionType,
        CancellationToken ct = default);
}
