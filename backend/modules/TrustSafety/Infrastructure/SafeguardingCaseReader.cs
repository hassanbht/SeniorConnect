using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingCaseReader : ISafeguardingCaseReader
{
    private static readonly SafeguardingStatus[] OpenStatuses =
    [
        SafeguardingStatus.Open,
        SafeguardingStatus.Assigned,
        SafeguardingStatus.UnderInvestigation,
        SafeguardingStatus.ActionTaken
    ];

    private readonly ISafeguardingDbContext _db;

    public SafeguardingCaseReader(ISafeguardingDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasOpenCaseAsSubjectAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.SafeguardingCases
            .AnyAsync(c => c.SubjectUserId == userId && OpenStatuses.Contains(c.Status), ct);
    }
}
