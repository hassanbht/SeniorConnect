using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Contracts;

/// <summary>
/// Cross-module read contract for verified user trust levels.
/// </summary>
public interface ITrustLevelReader
{
    Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

public sealed record UserContact(string DisplayName, string? Phone);

/// <summary>
/// Cross-module read contract for the minimal contact info BR-COMM-04
/// reveals to an assigned counterparty (never a raw DbSet&lt;User&gt;
/// reference from another module).
/// </summary>
public interface IUserContactReader
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// P6-03: Cross-module contract allowing Family provisioning to create a real User account
/// for a senior with default SeniorMode settings.
/// </summary>
public interface ISeniorAccountProvisioner
{
    Task<Result<Guid>> ProvisionSeniorUserAsync(
        string displayName,
        string? phone,
        Guid createdByUserId,
        CancellationToken ct = default);
}

public sealed record UserSessionDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId,
    string DisplayName);

/// <summary>
/// P6-04: Cross-module contract allowing Family module to issue an active JWT session
/// for a senior when redeeming a valid Zugangskarte.
/// </summary>
public interface IUserSessionIssuer
{
    Task<Result<UserSessionDto>> IssueSessionAsync(
        Guid userId,
        string? deviceLabel = null,
        CancellationToken ct = default);
}
