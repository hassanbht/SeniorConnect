namespace SeniorConnect.Modules.TrustSafety.Contracts;

/// <summary>
/// Cross-module contract for safety boundaries, buddy rules, and user blocks.
/// </summary>
public interface ISafetyBoundaryReader
{
    Task<bool> IsBlockedAsync(Guid userIdA, Guid userIdB, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(Guid userId, IReadOnlyCollection<Guid> candidateUserIds, CancellationToken ct = default);
    Task<bool> IsBuddyRequiredForLevel3Async(Guid volunteerUserId, CancellationToken ct = default);
}
