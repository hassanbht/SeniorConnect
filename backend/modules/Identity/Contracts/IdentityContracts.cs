namespace SeniorConnect.Modules.Identity.Contracts;

/// <summary>
/// Cross-module read contract for verified user trust levels.
/// </summary>
public interface ITrustLevelReader
{
    Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
