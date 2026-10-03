using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

/// <summary>
/// S-2/S-3 (Options B, approved 2026-09-30): the ONLY view of safeguarding
/// state that reaches coordinator surfaces. It answers one content-free
/// question — "who needs an operational follow-up?" — and nothing else.
/// No case id, restriction kind, reason, officer identity or severity passes
/// this boundary (BR-SG-05); SafeguardingLeakSweepTests and the S-3 tests keep
/// it that way.
/// </summary>
public sealed class SafeguardingOperationalReader(ISafeguardingDbContext db) : ISafeguardingOperationalReader
{
    public async Task<IReadOnlySet<Guid>> GetOpenFollowUpUserIdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var ids = await db.OperationalTasks
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId
                        && t.CompletedAtUtc == null
                        && t.CancelledAtUtc == null)
            .Select(t => t.SubjectUserId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }
}
