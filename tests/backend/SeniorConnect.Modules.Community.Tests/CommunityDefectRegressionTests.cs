using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

// 2026-09 Community Platform v1 defect fixes (P5-02..P5-09, P4-18, BR-COMM-05).
public sealed class CommunityDefectRegressionTests
{
    private const string FlaggedText = "Ruf mich an unter 0664 1234567, dann klären wir das.";

    private readonly SeniorConnectDbContext _db;
    private readonly CommunityService _service;

    public CommunityDefectRegressionTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
        _service = new CommunityService(_db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(_db),
            new FakeUserAccountStatusReader(), new FakeTrustLevelReader(), new FakeSafeguardingCaseReader());
    }

    // --- helpers ---------------------------------------------------------

    private async Task<CommunityGroup> SeedGroupAsync(Guid ownerId, GroupVisibility visibility = GroupVisibility.Public, Guid? orgId = null, int? maxMembers = null)
    {
        var group = CommunityGroup.Create(
            creatorUserId: ownerId,
            creatorType: orgId is null ? CreatorType.Individual : CreatorType.VerifiedOrganization,
            name: "Nachbarschaftstreff",
            description: "Wir treffen uns",
            type: GroupType.NeighborhoodGroup,
            visibility: visibility,
            organizationId: orgId,
            maxMembers: maxMembers).Value!;
        _db.CommunityGroups.Add(group);
        _db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        await _db.SaveChangesAsync();
        return group;
    }

    private async Task<GroupMembership> SeedMemberAsync(Guid groupId, Guid userId, GroupMemberRole role = GroupMemberRole.Member)
    {
        var membership = GroupMembership.JoinDirectly(groupId, userId);
        if (role != GroupMemberRole.Member)
        {
            membership.Promote(role, Guid.NewGuid());
        }

        _db.GroupMemberships.Add(membership);
        await _db.SaveChangesAsync();
        return membership;
    }

    private async Task<CommunityEvent> SeedEventAsync(
        Guid hostId, Guid? groupId = null, Guid? orgId = null, DateTimeOffset? startsAtUtc = null,
        EventRecurrenceFrequency recurrence = EventRecurrenceFrequency.None, DateTimeOffset? until = null, int? capacity = null)
    {
        var start = startsAtUtc ?? DateTimeOffset.UtcNow.AddDays(3);
        var ev = CommunityEvent.Create(hostId, "Spieleabend", "Karten und Kaffee", start, start.AddHours(2),
            groupId: groupId, organizationId: orgId, recurrenceFrequency: recurrence, recurrenceUntilUtc: until, capacity: capacity).Value!;
        _db.CommunityEvents.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    private async Task<Guid> SeedOrgWithCoordinatorAsync(Guid coordinatorId)
    {
        var org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        _db.Organizations.Add(org);
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync();
        return org.Id;
    }

    private static UpdateCommunityEventRequest Edit(string title = "Neuer Titel") => new(title, "Beschreibung", "general");

    // --- P5-03: events without an organization -------------------------------

    [Fact]
    public async Task Stranger_cannot_update_cancel_or_cancel_an_occurrence_of_an_event_without_organization()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var ev = await SeedEventAsync(hostId, recurrence: EventRecurrenceFrequency.Weekly, until: DateTimeOffset.UtcNow.AddDays(60));

        // Act
        var update = await _service.UpdateEventAsync(ev.Id, strangerId, Edit());
        var cancel = await _service.CancelEventAsync(ev.Id, strangerId, "weg");
        var cancelOccurrence = await _service.CancelEventOccurrenceAsync(ev.Id, strangerId, new CancelEventOccurrenceRequest(ev.StartsAtUtc, "weg"));

        // Assert
        update.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        cancel.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        cancelOccurrence.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _db.CommunityEvents.SingleAsync()).IsCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task Host_can_update_their_own_event_without_organization()
    {
        var hostId = Guid.NewGuid();
        var ev = await SeedEventAsync(hostId);

        var result = await _service.UpdateEventAsync(ev.Id, hostId, Edit("Spieleabend XL"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Spieleabend XL");
    }

    [Fact]
    public async Task Group_manager_can_cancel_a_group_event_they_did_not_host()
    {
        var ownerId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, moderatorId, GroupMemberRole.Moderator);
        var ev = await SeedEventAsync(ownerId, groupId: group.Id);

        var result = await _service.CancelEventAsync(ev.Id, moderatorId, "Krank");

        result.IsSuccess.Should().BeTrue();
    }

    // --- P5-03b: publishing under someone else's organization ---------------

    [Fact]
    public async Task Group_manager_cannot_publish_an_event_under_another_organizations_id()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        var foreignOrgId = await SeedOrgWithCoordinatorAsync(Guid.NewGuid());

        // Act
        var result = await _service.CreateEventAsync(ownerId, new CreateCommunityEventRequest(
            "Herbstfest", "Fest", DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(5).AddHours(2),
            GroupId: group.Id, OrganizationId: foreignOrgId));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task Plain_member_cannot_publish_an_event_in_a_group()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, memberId);

        var result = await _service.CreateEventAsync(memberId, new CreateCommunityEventRequest(
            "Treffen", "x", DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(5).AddHours(1), GroupId: group.Id));

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Ineligible_individual_cannot_publish_an_individual_event()
    {
        // Arrange — trust level 0 fails the ADR-025 individual gate
        var service = new CommunityService(_db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(_db),
            new FakeUserAccountStatusReader(), new FakeTrustLevelReader(trustLevel: 0), new FakeSafeguardingCaseReader());

        // Act
        var result = await service.CreateEventAsync(Guid.NewGuid(), new CreateCommunityEventRequest(
            "Treffen", "x", DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(5).AddHours(1)));

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    // --- P5-05: registrations ----------------------------------------------

    [Fact]
    public async Task Registering_twice_is_idempotent_and_keeps_one_row()
    {
        var ev = await SeedEventAsync(Guid.NewGuid());
        var userId = Guid.NewGuid();

        var first = await _service.RegisterForEventAsync(ev.Id, userId, new RegisterEventRequest());
        var second = await _service.RegisterForEventAsync(ev.Id, userId, new RegisterEventRequest());

        second.Value!.Id.Should().Be(first.Value!.Id);
        (await _db.EventRegistrations.CountAsync(r => r.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task Re_registering_after_cancelling_reuses_the_same_row()
    {
        var ev = await SeedEventAsync(Guid.NewGuid());
        var userId = Guid.NewGuid();
        var first = await _service.RegisterForEventAsync(ev.Id, userId, new RegisterEventRequest());
        await _service.CancelRegistrationAsync(ev.Id, userId);

        var again = await _service.RegisterForEventAsync(ev.Id, userId, new RegisterEventRequest());

        again.Value!.Id.Should().Be(first.Value!.Id);
        again.Value.Status.Should().Be(EventRsvpStatus.Going);
        (await _db.EventRegistrations.CountAsync(r => r.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task Cannot_register_for_an_event_that_is_over()
    {
        var ev = await SeedEventAsync(Guid.NewGuid(), startsAtUtc: DateTimeOffset.UtcNow.AddDays(-2));

        var result = await _service.RegisterForEventAsync(ev.Id, Guid.NewGuid(), new RegisterEventRequest());

        result.Error!.Code.Should().Be("EVENT_ENDED");
    }

    [Fact]
    public async Task Full_event_puts_the_next_registrant_on_the_waitlist()
    {
        var ev = await SeedEventAsync(Guid.NewGuid(), capacity: 1);
        await _service.RegisterForEventAsync(ev.Id, Guid.NewGuid(), new RegisterEventRequest());

        var result = await _service.RegisterForEventAsync(ev.Id, Guid.NewGuid(), new RegisterEventRequest());

        result.Value!.Status.Should().Be(EventRsvpStatus.Waitlisted);
        result.Value.WaitlistPosition.Should().Be(1);
    }

    [Fact]
    public async Task My_schedule_survives_legacy_duplicate_registrations()
    {
        // Arrange — two rows for the same (event, user), as written before the unique index
        var userId = Guid.NewGuid();
        var ev = await SeedEventAsync(Guid.NewGuid());
        _db.EventRegistrations.Add(EventRegistration.RegisterGoing(ev.Id, userId));
        _db.EventRegistrations.Add(EventRegistration.RegisterInterested(ev.Id, userId));
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetMyScheduleAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle(i => i.EventId == ev.Id);
    }

    // --- P5-04: recurring series -------------------------------------------

    [Fact]
    public async Task Recurring_series_stays_listed_after_its_first_occurrence_ended()
    {
        // Arrange — first occurrence two weeks ago, series runs another month
        var hostId = Guid.NewGuid();
        var series = await SeedEventAsync(hostId, startsAtUtc: DateTimeOffset.UtcNow.AddDays(-14),
            recurrence: EventRecurrenceFrequency.Weekly, until: DateTimeOffset.UtcNow.AddDays(30));
        await SeedEventAsync(hostId, startsAtUtc: DateTimeOffset.UtcNow.AddDays(-14)); // one-off, over
        var userId = Guid.NewGuid();
        _db.EventRegistrations.Add(EventRegistration.RegisterGoing(series.Id, userId));
        await _db.SaveChangesAsync();

        // Act
        var listed = await _service.GetEventsAsync(fromUtc: DateTimeOffset.UtcNow);
        var discovered = await new CommunityDiscoveryReader(_db).FindUpcomingEventsForDiscoveryAsync(CancellationToken.None);
        var schedule = await _service.GetMyScheduleAsync(userId);

        // Assert
        listed.Value!.Should().ContainSingle(e => e.Id == series.Id);
        discovered.Should().ContainSingle(e => e.EventId == series.Id);
        schedule.Value!.Should().ContainSingle(i => i.EventId == series.Id);
    }

    // --- P5-02: joining, rejoining, ranks ------------------------------------

    [Fact]
    public async Task Muted_member_rejoining_stays_muted_and_gets_no_second_row()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, memberId);
        await _service.MuteMemberAsync(group.Id, memberId, ownerId, DateTimeOffset.UtcNow.AddDays(1), "Spam");

        var result = await _service.JoinGroupAsync(group.Id, memberId);

        result.Error!.Code.Should().Be("MEMBERSHIP_MUTED");
        (await _db.GroupMemberships.CountAsync(m => m.UserId == memberId)).Should().Be(1);
    }

    [Theory]
    [InlineData(GroupMembershipStatus.Removed, "MEMBERSHIP_REMOVED")]
    [InlineData(GroupMembershipStatus.Banned, "MEMBERSHIP_BANNED")]
    public async Task Removed_or_banned_member_cannot_rejoin(GroupMembershipStatus status, string expectedCode)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, memberId);
        if (status == GroupMembershipStatus.Removed)
        {
            await _service.RemoveMemberAsync(group.Id, memberId, ownerId, "Regelverstoß");
        }
        else
        {
            await _service.BanMemberAsync(group.Id, memberId, ownerId, "Belästigung");
        }

        var result = await _service.JoinGroupAsync(group.Id, memberId);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(expectedCode);
        (await _db.GroupMemberships.CountAsync(m => m.UserId == memberId)).Should().Be(1);
    }

    [Fact]
    public async Task Member_who_left_rejoins_on_the_same_row()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        var original = await SeedMemberAsync(group.Id, memberId);
        await _service.LeaveGroupAsync(group.Id, memberId);

        var result = await _service.JoinGroupAsync(group.Id, memberId);

        result.Value!.Id.Should().Be(original.Id);
        result.Value.Status.Should().Be(GroupMembershipStatus.Active);
        (await _db.GroupMemberships.CountAsync(m => m.UserId == memberId)).Should().Be(1);
    }

    [Fact]
    public async Task Joining_a_full_group_is_rejected()
    {
        var group = await SeedGroupAsync(Guid.NewGuid(), maxMembers: 1);

        var result = await _service.JoinGroupAsync(group.Id, Guid.NewGuid());

        result.Error!.Code.Should().Be("GROUP_FULL");
    }

    [Fact]
    public async Task Moderator_cannot_ban_remove_or_mute_an_admin()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, moderatorId, GroupMemberRole.Moderator);
        await SeedMemberAsync(group.Id, adminId, GroupMemberRole.Admin);

        // Act
        var ban = await _service.BanMemberAsync(group.Id, adminId, moderatorId, "Machtkampf");
        var mute = await _service.MuteMemberAsync(group.Id, adminId, moderatorId, DateTimeOffset.UtcNow.AddDays(1), null);

        // Assert
        ban.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        mute.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _db.GroupMemberships.SingleAsync(m => m.UserId == adminId)).Status.Should().Be(GroupMembershipStatus.Active);
    }

    [Fact]
    public async Task Moderator_can_ban_a_plain_member()
    {
        var ownerId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, moderatorId, GroupMemberRole.Moderator);
        await SeedMemberAsync(group.Id, memberId);

        var result = await _service.BanMemberAsync(group.Id, memberId, moderatorId, "Spam");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Muted_member_cannot_post_in_a_group_thread()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, memberId);
        await _service.MuteMemberAsync(group.Id, memberId, ownerId, DateTimeOffset.UtcNow.AddDays(1), null);
        var thread = (await _service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;

        var result = await _service.PostMessageAsync(thread.Id, memberId, new PostMessageRequest("Hallo"));

        result.Error!.Code.Should().Be("MEMBERSHIP_MUTED");
    }

    // --- P5-02b: member list ---------------------------------------------------

    [Fact]
    public async Task Non_member_cannot_list_members_of_a_private_group()
    {
        var group = await SeedGroupAsync(Guid.NewGuid(), GroupVisibility.Private);

        var result = await _service.GetGroupMembersAsync(group.Id, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Active_member_can_list_members_of_a_private_group()
    {
        var ownerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId, GroupVisibility.Private);

        var result = await _service.GetGroupMembersAsync(group.Id, ownerId);

        result.Value!.Should().ContainSingle(m => m.UserId == ownerId);
    }

    [Fact]
    public async Task Listing_members_of_an_unknown_group_is_not_found()
    {
        var result = await _service.GetGroupMembersAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- P5-06 / P5-09: private-group events --------------------------------------

    [Fact]
    public async Task Private_group_event_is_hidden_from_non_members_everywhere()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId, GroupVisibility.Private);
        var ev = await SeedEventAsync(ownerId, groupId: group.Id);

        // Act
        var list = await _service.GetEventsAsync(requestingUserId: strangerId);
        var byId = await _service.GetEventByIdAsync(ev.Id, strangerId);
        var thread = await _service.GetOrCreateThreadAsync(ThreadContextType.Event, ev.Id, strangerId);
        var register = await _service.RegisterForEventAsync(ev.Id, strangerId, new RegisterEventRequest());
        var discovered = await new CommunityDiscoveryReader(_db).FindUpcomingEventsForDiscoveryAsync(CancellationToken.None);

        // Assert
        list.Value!.Should().BeEmpty();
        byId.Error!.Kind.Should().Be(ErrorKind.NotFound);
        thread.Error!.Kind.Should().Be(ErrorKind.NotFound);
        register.Error!.Kind.Should().Be(ErrorKind.NotFound);
        discovered.Should().BeEmpty();
    }

    [Fact]
    public async Task Private_group_event_is_visible_to_its_members()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId, GroupVisibility.Private);
        await SeedMemberAsync(group.Id, memberId);
        var ev = await SeedEventAsync(ownerId, groupId: group.Id);

        var list = await _service.GetEventsAsync(requestingUserId: memberId);
        var thread = await _service.GetOrCreateThreadAsync(ThreadContextType.Event, ev.Id, memberId);

        list.Value!.Should().ContainSingle(e => e.Id == ev.Id);
        thread.IsSuccess.Should().BeTrue();
    }

    // --- P5-09: reports ------------------------------------------------------------

    private async Task<(MessageThreadDto thread, ThreadMessageDto message)> SeedPublicThreadMessageAsync(Guid ownerId)
    {
        var group = await SeedGroupAsync(ownerId);
        var thread = (await _service.GetOrCreateThreadAsync(ThreadContextType.Group, group.Id, ownerId)).Value!;
        var message = (await _service.PostMessageAsync(thread.Id, ownerId, new PostMessageRequest("Wer kommt am Samstag?"))).Value!;
        return (thread, message);
    }

    [Fact]
    public async Task A_single_report_hides_a_message_only_for_the_reporter()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var bystanderId = Guid.NewGuid();
        var (thread, message) = await SeedPublicThreadMessageAsync(ownerId);

        // Act
        await _service.ReportMessageAsync(thread.Id, message.Id, reporterId, new ReportMessageRequest("gefällt mir nicht"));
        var reporterView = await _service.GetMessagesAsync(thread.Id, reporterId);
        var bystanderView = await _service.GetMessagesAsync(thread.Id, bystanderId);

        // Assert
        reporterView.Value!.Should().BeEmpty();
        bystanderView.Value!.Should().ContainSingle(m => m.Id == message.Id);
    }

    [Fact]
    public async Task Message_is_held_for_everyone_after_three_distinct_reporters()
    {
        var ownerId = Guid.NewGuid();
        var (thread, message) = await SeedPublicThreadMessageAsync(ownerId);

        for (var i = 0; i < 3; i++)
        {
            await _service.ReportMessageAsync(thread.Id, message.Id, Guid.NewGuid(), new ReportMessageRequest("Spam"));
        }

        var bystanderView = await _service.GetMessagesAsync(thread.Id, Guid.NewGuid());
        bystanderView.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Same_reporter_reporting_twice_counts_once()
    {
        var ownerId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var (thread, message) = await SeedPublicThreadMessageAsync(ownerId);

        for (var i = 0; i < 3; i++)
        {
            await _service.ReportMessageAsync(thread.Id, message.Id, reporterId, new ReportMessageRequest("Spam"));
        }

        (await _db.CommunityReports.CountAsync()).Should().Be(1);
        (await _service.GetMessagesAsync(thread.Id, Guid.NewGuid())).Value!.Should().ContainSingle();
    }

    [Fact]
    public async Task Reporting_a_non_existent_target_is_not_found()
    {
        var result = await _service.CreateReportAsync(Guid.NewGuid(), new CreateCommunityReportRequest(
            CommunityReportTargetType.Post, Guid.NewGuid(), CommunityReportCategory.Spam, "Spam"));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Report_group_is_derived_from_the_target_not_the_client()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        var post = (await _service.CreatePostAsync(group.Id, ownerId, new CreateCommunityPostRequest("Hallo", "Willkommen"))).Value!;

        // Act — the client lies about the group
        var result = await _service.CreateReportAsync(Guid.NewGuid(), new CreateCommunityReportRequest(
            CommunityReportTargetType.Post, post.Id, CommunityReportCategory.Spam, "Spam", GroupId: Guid.NewGuid()));

        // Assert
        result.Value!.GroupId.Should().Be(group.Id);
    }

    // --- P4-18 / BR-COMM-05: screening of titles, names, events ----------------------

    [Fact]
    public async Task Post_with_a_flagged_title_is_held_for_review()
    {
        var ownerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);

        var result = await _service.CreatePostAsync(group.Id, ownerId, new CreateCommunityPostRequest(FlaggedText, "Harmloser Text"));

        result.Value!.Status.Should().Be(CommunityPostStatus.PendingReview);
    }

    [Fact]
    public async Task Group_with_a_flagged_description_is_held_and_not_discoverable()
    {
        var result = await _service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual,
            new CreateCommunityGroupRequest("Strickrunde", FlaggedText, GroupType.InterestGroup));

        result.Value!.Status.Should().Be(GroupStatus.Draft);
        (await _service.GetGroupsAsync()).Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Event_with_a_flagged_description_is_visible_only_to_its_host()
    {
        // Arrange
        var hostId = Guid.NewGuid();

        // Act
        var created = await _service.CreateEventAsync(hostId, new CreateCommunityEventRequest(
            "Treffen", FlaggedText, DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddHours(1)));
        var strangerView = await _service.GetEventByIdAsync(created.Value!.Id, Guid.NewGuid());
        var hostView = await _service.GetEventByIdAsync(created.Value.Id, hostId);

        // Assert
        created.Value.IsPendingReview.Should().BeTrue();
        strangerView.Error!.Kind.Should().Be(ErrorKind.NotFound);
        hostView.IsSuccess.Should().BeTrue();
    }

    // --- BR-COMM-05: review queue ---------------------------------------------------------

    [Fact]
    public async Task Group_manager_sees_and_approves_a_held_post_in_their_group()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        await SeedMemberAsync(group.Id, memberId);
        var post = (await _service.CreatePostAsync(group.Id, memberId, new CreateCommunityPostRequest("Hallo", FlaggedText))).Value!;

        // Act
        var queue = await _service.GetModerationQueueAsync(ownerId, isPlatformModerator: false);
        var resolve = await _service.ResolveModerationItemAsync(ownerId, false, ModerationQueueItemType.Post, post.Id,
            new ResolveModerationItemRequest(ModerationResolution.Approve));

        // Assert
        queue.Value!.Should().ContainSingle(i => i.ItemType == ModerationQueueItemType.Post && i.ItemId == post.Id);
        resolve.IsSuccess.Should().BeTrue();
        (await _db.CommunityPosts.SingleAsync()).Status.Should().Be(CommunityPostStatus.Visible);
        (await _db.CommunityModerationActions.AnyAsync(a => a.Action == CommunityModerationActionType.ApproveHeldContent)).Should().BeTrue();
    }

    [Fact]
    public async Task Outsider_neither_sees_nor_can_resolve_another_groups_held_post()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        var post = (await _service.CreatePostAsync(group.Id, ownerId, new CreateCommunityPostRequest("Hallo", FlaggedText))).Value!;

        var queue = await _service.GetModerationQueueAsync(outsiderId, isPlatformModerator: false);
        var resolve = await _service.ResolveModerationItemAsync(outsiderId, false, ModerationQueueItemType.Post, post.Id,
            new ResolveModerationItemRequest(ModerationResolution.Approve));

        queue.Value!.Should().BeEmpty();
        resolve.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Group_manager_cannot_approve_their_own_held_post()
    {
        var ownerId = Guid.NewGuid();
        var group = await SeedGroupAsync(ownerId);
        var post = (await _service.CreatePostAsync(group.Id, ownerId, new CreateCommunityPostRequest("Hallo", FlaggedText))).Value!;

        var resolve = await _service.ResolveModerationItemAsync(ownerId, false, ModerationQueueItemType.Post, post.Id,
            new ResolveModerationItemRequest(ModerationResolution.Approve));

        resolve.Error!.Code.Should().Be("OWN_CONTENT_REVIEW");
    }

    [Fact]
    public async Task Platform_moderator_rejects_a_held_group_which_becomes_blocked()
    {
        var created = (await _service.CreateGroupAsync(Guid.NewGuid(), CreatorType.Individual,
            new CreateCommunityGroupRequest("Strickrunde", FlaggedText, GroupType.InterestGroup))).Value!;

        var queue = await _service.GetModerationQueueAsync(Guid.NewGuid(), isPlatformModerator: true);
        var resolve = await _service.ResolveModerationItemAsync(Guid.NewGuid(), true, ModerationQueueItemType.Group, created.Id,
            new ResolveModerationItemRequest(ModerationResolution.Reject, "Kontaktdaten"));

        queue.Value!.Should().ContainSingle(i => i.ItemType == ModerationQueueItemType.Group);
        resolve.IsSuccess.Should().BeTrue();
        (await _db.CommunityGroups.SingleAsync()).Status.Should().Be(GroupStatus.Blocked);
    }

    [Fact]
    public async Task Upholding_a_message_report_takes_the_message_down_and_closes_the_report()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (thread, message) = await SeedPublicThreadMessageAsync(ownerId);
        await _service.ReportMessageAsync(thread.Id, message.Id, Guid.NewGuid(), new ReportMessageRequest("Spam"));
        var reportId = (await _db.CommunityReports.SingleAsync()).Id;
        var moderatorId = Guid.NewGuid();

        // Act
        var resolve = await _service.ResolveModerationItemAsync(moderatorId, true, ModerationQueueItemType.Report, reportId,
            new ResolveModerationItemRequest(ModerationResolution.Reject));

        // Assert
        resolve.IsSuccess.Should().BeTrue();
        (await _db.CommunityReports.SingleAsync()).Status.Should().Be(CommunityReportStatus.Resolved);
        (await _service.GetMessagesAsync(thread.Id, Guid.NewGuid())).Value!.Should().BeEmpty();
    }
}
