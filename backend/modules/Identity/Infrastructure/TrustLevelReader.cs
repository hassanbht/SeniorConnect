using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class TrustLevelReader : ITrustLevelReader
{
    private readonly IIdentityDbContext _db;

    public TrustLevelReader(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.TrustLevelSnapshots
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ComputedAtUtc)
            .Select(s => (int)s.Level)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var list = await _db.TrustLevelSnapshots
            .Where(s => userIds.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Level = g.OrderByDescending(s => s.ComputedAtUtc).Select(s => (int)s.Level).FirstOrDefault()
            })
            .ToListAsync(ct);

        return list.ToDictionary(x => x.UserId, x => x.Level);
    }
}
