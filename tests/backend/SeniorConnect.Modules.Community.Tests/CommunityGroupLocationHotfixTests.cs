using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>
/// Security hotfix (docs/security/hotfix-matching-location.md §7): a group's
/// meeting place can be a private home, so CommunityGroupDto only carries its
/// ~1 km grid cell. The stored value stays exact and group features keep working.
/// </summary>
public sealed class CommunityGroupLocationHotfixTests : IDisposable
{
    private const double ExactLat = 47.26917;
    private const double ExactLon = 11.40412;

    private readonly SeniorConnectDbContext _db;
    private readonly CommunityService _service;

    public CommunityGroupLocationHotfixTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
        _service = new CommunityService(_db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(_db),
            new FakeUserAccountStatusReader(), new FakeTrustLevelReader(), new FakeSafeguardingCaseReader());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GroupDto_ForAnotherUser_CarriesOnlyTheGridCell_NotTheExactPoint()
    {
        var ownerId = Guid.NewGuid();
        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            creatorType: CreatorType.Individual,
            name: "Nachbarschaftstreff",
            description: "Wir treffen uns",
            type: GroupType.NeighborhoodGroup,
            postalCode: "6020",
            latitude: ExactLat,
            longitude: ExactLon).Value!;
        _db.CommunityGroups.Add(group);
        _db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        await _db.SaveChangesAsync();

        var single = await _service.GetGroupByIdAsync(group.Id, requestingUserId: Guid.NewGuid());
        var list = await _service.GetGroupsAsync();

        single.IsSuccess.Should().BeTrue();
        single.Value!.Latitude.Should().Be(47.27);
        single.Value!.Longitude.Should().Be(11.40);
        single.Value!.PostalCode.Should().Be("6020", "the group keeps its area");

        var json = JsonSerializer.Serialize(new object[] { single.Value!, list.Value! });
        json.Should().NotContain("47.26917").And.NotContain("11.40412");

        var stored = await _db.CommunityGroups.AsNoTracking().SingleAsync();
        stored.Latitude.Should().Be(ExactLat, "only the response is reduced, never the stored value");
    }
}
