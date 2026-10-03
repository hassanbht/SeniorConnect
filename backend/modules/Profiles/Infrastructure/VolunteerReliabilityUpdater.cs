using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class VolunteerReliabilityUpdater : IVolunteerReliabilityUpdater
{
    private readonly IProfilesDbContext _db;

    public VolunteerReliabilityUpdater(IProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<decimal?> RecordOutcomeAsync(Guid volunteerUserId, bool wasReliable, CancellationToken cancellationToken = default)
    {
        var profile = await _db.VolunteerProfiles
            .FirstOrDefaultAsync(p => p.UserId == volunteerUserId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var previousScore = profile.ReliabilityScore;
        profile.RecordCompletionOutcome(wasReliable);
        await _db.SaveChangesAsync(cancellationToken);

        return previousScore;
    }

    public async Task RestoreScoreAsync(Guid volunteerUserId, decimal? score, CancellationToken cancellationToken = default)
    {
        var profile = await _db.VolunteerProfiles
            .FirstOrDefaultAsync(p => p.UserId == volunteerUserId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        profile.UpdateReliability(score);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
