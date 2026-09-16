using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Contracts;

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

        return await _db.CommunityEvents
            .Where(e => !e.IsDeleted && !e.IsCancelled && e.EndsAtUtc >= now)
            .Select(e => new CommunityEventDiscoveryRow(e.Id, e.Title, e.Category, e.LocationPostalCode, e.StartsAtUtc))
            .ToListAsync(ct);
    }
}
