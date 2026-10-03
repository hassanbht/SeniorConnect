using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>
/// Shared BR-COMM-06 / ADR-025 eligibility-gate test doubles for
/// <see cref="SeniorConnect.Modules.Community.Infrastructure.CommunityService"/>.
/// Default to "eligible" (active account, trust level 1, no open safeguarding
/// case) so tests creating groups as a plain individual don't each need to
/// wire this up; pass non-default values to exercise a rejection path.
/// </summary>
public sealed class FakeUserAccountStatusReader(bool isActive = true) : IUserAccountStatusReader
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(isActive);
}

public sealed class FakeTrustLevelReader(int trustLevel = 1) : ITrustLevelReader
{
    public Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(trustLevel);

    public Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(userIds.ToDictionary(id => id, _ => trustLevel));
}

public sealed class FakeSafeguardingCaseReader(bool hasOpenCase = false) : ISafeguardingCaseReader
{
    public Task<bool> HasOpenCaseAsSubjectAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(hasOpenCase);
}
