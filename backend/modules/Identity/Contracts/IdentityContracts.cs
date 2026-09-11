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
