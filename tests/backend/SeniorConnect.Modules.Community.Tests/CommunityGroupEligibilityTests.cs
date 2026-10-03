using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>
/// BR-COMM-06 / ADR-025: server-side eligibility gate for individual
/// (non-organization) community group creators.
/// </summary>
public sealed class CommunityGroupEligibilityTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
    }

    private static CommunityService CreateService(
        SeniorConnectDbContext db,
        bool isActive = true,
        int trustLevel = 1,
        bool hasOpenSafeguardingCase = false) =>
        new(db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(db),
            new FakeUserAccountStatusReader(isActive),
            new FakeTrustLevelReader(trustLevel),
            new FakeSafeguardingCaseReader(hasOpenSafeguardingCase));

    private static CreateCommunityGroupRequest ValidRequest() => new(
        Name: "Deutsch-Konversation für Senior:innen",
        Description: "Wöchentliches Treffen zum gemeinsamen Deutschüben.",
        Type: GroupType.LearningGroup);

    [Fact]
    public async Task Individual_with_active_account_and_trust_level_can_create_group()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual, ValidRequest());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Individual_with_inactive_account_cannot_create_group()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db, isActive: false);

        var result = await service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual, ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Individual_below_minimum_trust_level_cannot_create_group()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db, trustLevel: 0);

        var result = await service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual, ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Individual_with_open_safeguarding_case_cannot_create_group()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db, hasOpenSafeguardingCase: true);

        var result = await service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual, ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Individual_banned_from_another_group_cannot_create_a_new_group()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var otherGroupId = Guid.NewGuid();
        var membership = GroupMembership.JoinDirectly(otherGroupId, userId);
        membership.Ban(Guid.NewGuid(), "Repeated harassment");
        db.GroupMemberships.Add(membership);
        await db.SaveChangesAsync();

        var result = await service.CreateGroupAsync(userId, CreatorType.Individual, ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Volunteer_creator_type_is_also_subject_to_the_individual_eligibility_gate()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db, isActive: false);

        var result = await service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Volunteer, ValidRequest());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
