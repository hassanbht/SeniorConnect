using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Contracts;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Infrastructure;

public sealed class CommunityDiscoveryReader : ICommunityDiscoveryReader
{
    private readonly ICommunityDbContext _db;

    public CommunityDiscoveryReader(ICommunityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CommunityEventDiscoveryRow>> FindUpcomingEventsForDiscoveryAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var publicGroupIds = _db.CommunityGroups
            .Where(g => g.Visibility == GroupVisibility.Public)
            .Select(g => g.Id);

        // P5-04: recurring series stay discoverable until their recurrence window ends.
        // P5-06: anonymous discovery shows no private-group and no held events.
        return await _db.CommunityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && !e.IsCancelled && !e.IsPendingReview)
            .Where(e => e.GroupId == null || publicGroupIds.Contains(e.GroupId.Value))
            .Where(CommunityEvent.IsRunningOnOrAfter(now))
            .Select(e => new CommunityEventDiscoveryRow(e.Id, e.Title, e.Category, e.LocationPostalCode, e.StartsAtUtc))
            .ToListAsync(ct);
    }
}
