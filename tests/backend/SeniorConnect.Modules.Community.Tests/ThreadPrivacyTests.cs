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

// BR-COMM-05 / ADR-020: a non-Public group's discussion thread (e.g. a
// PrivateInvitation caregiver circle) must only be readable/postable by its
// active members — 2026-09 fix, previously any authenticated user could
// read any thread by id regardless of group membership.
public sealed class ThreadPrivacyTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
    }

    private static CommunityService CreateService(SeniorConnectDbContext db) =>
        new(db, new LocalMessageModerationService(), new OrganizationCoordinatorReader(db));

    [Fact]
    public async Task Non_member_cannot_read_messages_in_a_private_group_thread()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            title: "Private Family Caregiver Circle",
            description: "Confidential group",
            scope: GroupVisibilityScope.PrivateInvitation,
            joinPolicy: GroupJoinPolicy.InviteOnly,
            organizationId: Guid.NewGuid()).Value!;
        db.CommunityGroups.Add(group);
        db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        await db.SaveChangesAsync();

        var thread = (await service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;
        await service.PostMessageAsync(thread.Id, ownerId, new PostMessageRequest("Owner-only note"));

        var strangerResult = await service.GetMessagesAsync(thread.Id, strangerId);

        strangerResult.IsFailure.Should().BeTrue();
        strangerResult.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Non_member_cannot_post_to_a_private_group_thread()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            title: "Private Family Caregiver Circle",
            description: "Confidential group",
            scope: GroupVisibilityScope.PrivateInvitation,
            joinPolicy: GroupJoinPolicy.InviteOnly,
            organizationId: Guid.NewGuid()).Value!;
        db.CommunityGroups.Add(group);
        db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        await db.SaveChangesAsync();

        var thread = (await service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;

        var postResult = await service.PostMessageAsync(thread.Id, strangerId, new PostMessageRequest("I shouldn't be here"));

        postResult.IsFailure.Should().BeTrue();
        postResult.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Active_member_can_read_messages_in_a_private_group_thread()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            title: "Private Family Caregiver Circle",
            description: "Confidential group",
            scope: GroupVisibilityScope.OrganizationMembers,
            joinPolicy: GroupJoinPolicy.RequestApproval,
            organizationId: Guid.NewGuid()).Value!;
        db.CommunityGroups.Add(group);
        db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        db.GroupMemberships.Add(GroupMembership.JoinDirectly(group.Id, memberId));
        await db.SaveChangesAsync();

        var thread = (await service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;
        await service.PostMessageAsync(thread.Id, ownerId, new PostMessageRequest("Members-only note"));

        var memberResult = await service.GetMessagesAsync(thread.Id, memberId);

        memberResult.IsSuccess.Should().BeTrue();
        memberResult.Value!.Should().ContainSingle(m => m.Content == "Members-only note");
    }

    [Fact]
    public async Task Anyone_can_read_a_public_groups_thread_without_membership()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var ownerId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            title: "Neighborhood Chess Club",
            description: "Open to all",
            scope: GroupVisibilityScope.Public,
            joinPolicy: GroupJoinPolicy.Open,
            organizationId: Guid.NewGuid()).Value!;
        db.CommunityGroups.Add(group);
        db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        await db.SaveChangesAsync();

        var thread = (await service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;
        await service.PostMessageAsync(thread.Id, ownerId, new PostMessageRequest("Welcome everyone"));

        var visitorResult = await service.GetMessagesAsync(thread.Id, visitorId);

        visitorResult.IsSuccess.Should().BeTrue();
        visitorResult.Value!.Should().ContainSingle(m => m.Content == "Welcome everyone");
    }

    [Fact]
    public async Task Anyone_can_read_an_events_thread_regardless_of_registration()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var visitorId = Guid.NewGuid();

        var ev = CommunityEvent.Create(
            hostUserId: hostId,
            title: "Herbstfest",
            description: "Public event",
            startsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            endsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            organizationId: Guid.NewGuid()).Value!;
        db.CommunityEvents.Add(ev);
        await db.SaveChangesAsync();

        var thread = (await service.GetOrCreateThreadAsync(ThreadContextType.Event, ev.Id, hostId)).Value!;
        await service.PostMessageAsync(thread.Id, hostId, new PostMessageRequest("Looking forward to it!"));

        var visitorResult = await service.GetMessagesAsync(thread.Id, visitorId);

        visitorResult.IsSuccess.Should().BeTrue();
        visitorResult.Value!.Should().ContainSingle(m => m.Content == "Looking forward to it!");
    }
}
