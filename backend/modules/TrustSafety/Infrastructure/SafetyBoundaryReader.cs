using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafetyBoundaryReader : ISafetyBoundaryReader
{
    private readonly ITrustSafetyDbContext _db;

    public SafetyBoundaryReader(ITrustSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsBlockedAsync(Guid userIdA, Guid userIdB, CancellationToken ct = default)
    {
        return await _db.UserBlocks
            .AnyAsync(b => (b.BlockingUserId == userIdA && b.BlockedUserId == userIdB)
                        || (b.BlockingUserId == userIdB && b.BlockedUserId == userIdA), ct);
    }

    public async Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(Guid userId, IReadOnlyCollection<Guid> candidateUserIds, CancellationToken ct = default)
    {
        if (candidateUserIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return await _db.UserBlocks
            .Where(b => (b.BlockingUserId == userId && candidateUserIds.Contains(b.BlockedUserId))
                     || (candidateUserIds.Contains(b.BlockingUserId) && b.BlockedUserId == userId))
            .Select(b => b.BlockingUserId == userId ? b.BlockedUserId : b.BlockingUserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> IsBuddyRequiredForLevel3Async(Guid volunteerUserId, CancellationToken ct = default)
    {
        var buddy = await _db.BuddyAssignments
            .FirstOrDefaultAsync(b => b.VolunteerUserId == volunteerUserId, ct);

        if (buddy is null)
        {
            return true;
        }

        return buddy.IsBuddyRequired && !buddy.IsWaived && buddy.AssignedBuddyVolunteerUserId == null;
    }
}
