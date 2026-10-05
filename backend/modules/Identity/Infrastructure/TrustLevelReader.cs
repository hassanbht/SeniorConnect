using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// ADR-030: effective trust is computed from live verification facts at every
/// trust-consuming boundary. Nothing is authoritative but the facts.
///
/// <para><b>Why not the snapshot.</b> The old implementation returned the
/// login-time <c>TrustLevelSnapshot</c>, so a verification revoked or expired
/// after login kept authorizing until the next login. A login-time snapshot
/// without an invalidation rule is a cache with an unknown TTL.</para>
///
/// <para>The snapshot table is still written on login as audit evidence, but
/// no authorization path reads it as authority.</para>
/// </summary>
public sealed class TrustLevelReader : ITrustLevelReader
{
    private readonly IIdentityDbContext _db;
    private readonly ITrustLevelCalculator _calculator;

    public TrustLevelReader(IIdentityDbContext db, ITrustLevelCalculator? calculator = null)
    {
        _db = db;
        _calculator = calculator ?? new TrustLevelCalculator();
    }

    public async Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

        if (user is null || user.Status != UserStatus.Active)
        {
            // ADR-030: a missing or deactivated account resolves to no trust.
            return 0;
        }

        var verifications = await LoadVerificationsAsync(new[] { userId }, ct);
        return _calculator.Evaluate(user, verifications).Level;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .ToListAsync(ct);

        var verifications = await LoadVerificationsAsync(userIds, ct);

        return users.ToDictionary(u => u.Id, u =>
        {
            if (u.Status != UserStatus.Active)
            {
                return 0;
            }

            return _calculator.Evaluate(
                u,
                verifications.Where(v => v.UserId == u.Id).ToList()).Level;
        });
    }

    private async Task<List<Verification>> LoadVerificationsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct) =>
        await _db.Verifications
            .AsNoTracking()
            .Where(v => userIds.Contains(v.UserId))
            .ToListAsync(ct);
}
