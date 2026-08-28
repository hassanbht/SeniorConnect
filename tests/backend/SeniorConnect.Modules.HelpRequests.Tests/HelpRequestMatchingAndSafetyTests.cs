using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class HelpRequestMatchingAndSafetyTests
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

    [Fact]
    public async Task AcceptHelpRequest_WhenVolunteerTrustLevelIsInsufficient_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome).Value!;

        db.HelpRequests.Add(request);

        // Volunteer has only Trust Level 1
        var snapshot = TrustLevelSnapshot.Create(volunteerId, 1, "{\"reason\":\"basic_phone\"}");
        db.TrustLevelSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader, safetyReader);

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TRUST_LEVEL_INSUFFICIENT");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task AcceptHelpRequest_WhenBuddyIsRequiredForLevel3_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome).Value!;

        db.HelpRequests.Add(request);

        // Volunteer has Trust Level 3 but is new (0 level-3 completed, no buddy assigned)
        var snapshot = TrustLevelSnapshot.Create(volunteerId, 3, "{\"reason\":\"police_record_check\"}");
        db.TrustLevelSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var trustReader2 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader2 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader2, safetyReader2);

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BUDDY_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task AcceptHelpRequest_WhenUserIsBlocked_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        db.HelpRequests.Add(request);

        var snapshot = TrustLevelSnapshot.Create(volunteerId, 2, "{}");
        db.TrustLevelSnapshots.Add(snapshot);

        // Senior has blocked volunteer
        var block = UserBlock.Create(seniorId, volunteerId, "Personal boundary preference");
        db.UserBlocks.Add(block);
        await db.SaveChangesAsync();

        var trustReader3 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader3 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader3, safetyReader3);

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task MatchingService_ExcludesBlockedVolunteers_AndFlagsLowTrust()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var blockedVolId = Guid.NewGuid();
        var lowTrustVolId = Guid.NewGuid();
        var eligibleVolId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        db.HelpRequests.Add(request);

        var p1 = VolunteerProfile.Create(blockedVolId, "Blocked Vol");
        p1.UpdateStatus(true);
        var p2 = VolunteerProfile.Create(lowTrustVolId, "Low Trust Vol");
        p2.UpdateStatus(true);
        var p3 = VolunteerProfile.Create(eligibleVolId, "Eligible Vol");
        p3.UpdateStatus(true);

        db.VolunteerProfiles.AddRange(p1, p2, p3);

        // Block record
        db.UserBlocks.Add(UserBlock.Create(seniorId, blockedVolId, "Blocked"));

        // Trust levels: lowTrust = 1, eligible = 3
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(lowTrustVolId, 1, "{}"));
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(eligibleVolId, 3, "{}"));
        await db.SaveChangesAsync();

        var trustReader4 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader4 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var matchingService = new MatchingService(db, db, trustReader4, safetyReader4);

        var candidatesResult = await matchingService.FindCandidatesAsync(request.Id);

        candidatesResult.IsSuccess.Should().BeTrue();
        var candidates = candidatesResult.Value!;

        // Blocked volunteer must not appear at all
        candidates.Should().NotContain(c => c.VolunteerUserId == blockedVolId);

        // Low trust volunteer is marked ineligible
        var lowTrustCandidate = candidates.FirstOrDefault(c => c.VolunteerUserId == lowTrustVolId);
        lowTrustCandidate.Should().NotBeNull();
        lowTrustCandidate!.IsEligible.Should().BeFalse();
        lowTrustCandidate.IneligibilityReasons.Should().Contain(r => r.Contains("below required level"));

        // Eligible volunteer is marked eligible
        var eligibleCandidate = candidates.FirstOrDefault(c => c.VolunteerUserId == eligibleVolId);
        eligibleCandidate.Should().NotBeNull();
        eligibleCandidate!.IsEligible.Should().BeTrue();
    }
}
