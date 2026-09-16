using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// P3-19 / P3-20: a no-show nudges reliability down (snapshotting the prior
/// value), and a successful dispute REVERTS it exactly — not just a re-nudge
/// back up, which would leave a volunteer worse off than before the
/// (incorrect) no-show report.
/// </summary>
public sealed class NoShowDisputeAndReliabilityTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new TestTenantContext());
    }

    private static HelpRequestService CreateService(SeniorConnectDbContext db) => new(
        db,
        new ActivitySafetyPolicy(),
        new TrustLevelReader(db),
        new SafetyBoundaryReader(db),
        new UserContactReader(db),
        new VolunteerReliabilityUpdater(db));

    [Fact]
    public async Task MarkNoShow_NudgesReliabilityDown_ThenDispute_RestoresExactPriorScore()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var profile = VolunteerProfile.Create(volunteerId);
        profile.RecordCompletionOutcome(true); // history of 3 completions -> a solid score
        profile.RecordCompletionOutcome(true);
        profile.RecordCompletionOutcome(true);
        var scoreBeforeNoShow = profile.ReliabilityScore;
        db.VolunteerProfiles.Add(profile);

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(-2),
            scheduledEndUtc: now.AddHours(-1),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;
        request.Publish();
        request.Assign(volunteerId, request.RowVersion);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var noShowResult = await service.MarkNoShowAsync(
            request.Id, seniorId, new NoShowHelpRequestRequest("Never arrived"));
        noShowResult.IsSuccess.Should().BeTrue();

        var profileAfterNoShow = await db.VolunteerProfiles.SingleAsync(p => p.UserId == volunteerId);
        profileAfterNoShow.ReliabilityScore.Should().BeLessThan(scoreBeforeNoShow!.Value,
            "an uncontested no-show must nudge reliability down");

        var disputeResult = await service.DisputeNoShowAsync(
            request.Id, volunteerId, new DisputeNoShowRequest("Senior cancelled by phone, coordinator confirmed"));

        disputeResult.IsSuccess.Should().BeTrue();

        var profileAfterDispute = await db.VolunteerProfiles.SingleAsync(p => p.UserId == volunteerId);
        profileAfterDispute.ReliabilityScore.Should().Be(scoreBeforeNoShow,
            "a successful dispute reverts the score exactly, not just re-nudges it");
    }

    [Fact]
    public async Task DisputeNoShow_Fails_WhenAlreadyDisputed()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.VolunteerProfiles.Add(VolunteerProfile.Create(volunteerId));

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(-2),
            scheduledEndUtc: now.AddHours(-1),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;
        request.Publish();
        request.Assign(volunteerId, request.RowVersion);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.MarkNoShowAsync(request.Id, seniorId, new NoShowHelpRequestRequest());

        var first = await service.DisputeNoShowAsync(request.Id, volunteerId, new DisputeNoShowRequest("Reason one"));
        first.IsSuccess.Should().BeTrue();

        var second = await service.DisputeNoShowAsync(request.Id, volunteerId, new DisputeNoShowRequest("Reason two"));
        second.IsFailure.Should().BeTrue("a no-show can only be disputed once");
    }
}
