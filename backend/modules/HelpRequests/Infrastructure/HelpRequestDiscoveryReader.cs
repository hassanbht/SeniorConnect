using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Contracts;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class HelpRequestDiscoveryReader : IHelpRequestDiscoveryReader
{
    private readonly IHelpRequestsDbContext _db;

    public HelpRequestDiscoveryReader(IHelpRequestsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NearbyHelpRequestRow>> FindOpenRequestsWithLocationAsync(CancellationToken ct)
    {
        return await _db.HelpRequests
            .Where(r => r.Status == HelpRequestStatus.Open && r.Latitude != null && r.Longitude != null)
            .Select(r => new NearbyHelpRequestRow(r.Id, r.CategoryId, r.Latitude!.Value, r.Longitude!.Value))
            .ToListAsync(ct);
    }
}
