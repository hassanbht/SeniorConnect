using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingRestrictionReader : ISafeguardingRestrictionReader
{
    private readonly ISafeguardingDbContext _db;

    public SafeguardingRestrictionReader(ISafeguardingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ActiveRestriction>> GetActiveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }
        var ids = userIds.Distinct().ToList();
        return await _db.SafeguardingRestrictions
            .AsNoTracking()
            .Where(r => r.LiftedAtUtc == null
                        && (ids.Contains(r.SubjectUserId)
                            || (r.CounterpartUserId != null && ids.Contains(r.CounterpartUserId.Value))))
            .Select(r => new ActiveRestriction(
                r.SubjectUserId, r.CounterpartUserId, r.BlocksDiscovery, r.BlocksLocation, r.BlocksContact, r.BlocksAssignment))
            .ToListAsync(ct);
    }
}
