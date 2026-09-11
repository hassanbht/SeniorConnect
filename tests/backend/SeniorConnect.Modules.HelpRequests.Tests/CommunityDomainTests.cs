using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class CommunityDomainTests
{
    [Fact]
    public void CommunityGroup_OpenJoin_DirectlyAddsActiveMember()
    {
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var groupResult = CommunityGroup.Create(
            creatorUserId: creatorId,
            title: "Senioren Schachklub",
            description: "Wöchentlicher Schachtreff im Gemeindezentrum",
            category: "games",
            scope: GroupVisibilityScope.Public,
            joinPolicy: GroupJoinPolicy.Open,
            organizationId: Guid.NewGuid(), // BR-COMM-06 / ADR-020: org required
            locationPostalCode: "1010");

        Assert.True(groupResult.IsSuccess);
        var group = groupResult.Value!;
        Assert.Equal(GroupJoinPolicy.Open, group.JoinPolicy);

        var membership = GroupMembership.JoinDirectly(group.Id, memberId);
        Assert.Equal(GroupMembershipStatus.Active, membership.Status);
        Assert.Equal(GroupMemberRole.Member, membership.Role);
    }

    [Fact]
    public void CommunityGroup_ApprovalJoin_CreatesPendingRequestThatCanBeApprovedOrRejected()
    {
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var groupResult = CommunityGroup.Create(
            creatorUserId: creatorId,
            title: "Nachbarschaftsgarten",
            description: "Gemeinsames Gärtnern",
            joinPolicy: GroupJoinPolicy.RequestApproval,
            organizationId: Guid.NewGuid()); // BR-COMM-06 / ADR-020: org required

        Assert.True(groupResult.IsSuccess);
        var group = groupResult.Value!;

        var membership = GroupMembership.RequestToJoin(group.Id, memberId);
        Assert.Equal(GroupMembershipStatus.PendingApproval, membership.Status);

        var approveResult = membership.Approve(adminId);
        Assert.True(approveResult.IsSuccess);
        Assert.Equal(GroupMembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void CommunityEvent_CreationAndRescheduling_UpdatesScheduleCorrectly()
    {
        var hostId = Guid.NewGuid();
        var starts = DateTimeOffset.UtcNow.AddDays(2);
        var ends = starts.AddHours(2);

        var eventResult = CommunityEvent.Create(
            hostUserId: hostId,
            title: "Spielenachmittag",
            description: "Karten- und Brettspiele für Jung und Alt",
            startsAtUtc: starts,
            endsAtUtc: ends,
            category: "social",
            capacity: 10);

        Assert.True(eventResult.IsSuccess);
        var ev = eventResult.Value!;
        Assert.Equal(10, ev.Capacity);
        Assert.False(ev.IsCancelled);

        var newStarts = starts.AddDays(1);
        var newEnds = newStarts.AddHours(2);
        var rescheduleResult = ev.Reschedule(newStarts, newEnds, hostId);
        Assert.True(rescheduleResult.IsSuccess);
        Assert.Equal(newStarts, ev.StartsAtUtc);
    }

    [Fact]
    public void EventRegistration_GoingAndPromotionLifecycle_WorksCorrectly()
    {
        var eventId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var reg1 = EventRegistration.RegisterGoing(eventId, user1);
        Assert.Equal(EventRsvpStatus.Going, reg1.Status);
        Assert.Null(reg1.WaitlistPosition);

        var reg2 = EventRegistration.RegisterWaitlist(eventId, user2, waitlistPosition: 1);
        Assert.Equal(EventRsvpStatus.Waitlisted, reg2.Status);
        Assert.Equal(1, reg2.WaitlistPosition);

        // Cancel user1
        reg1.Cancel();
        Assert.Equal(EventRsvpStatus.Cancelled, reg1.Status);
        Assert.NotNull(reg1.CancelledAtUtc);

        // Promote user2
        reg2.PromoteToGoing();
        Assert.Equal(EventRsvpStatus.Going, reg2.Status);
        Assert.Null(reg2.WaitlistPosition);
    }

    [Fact]
    public void ContextualDiscussion_PostAndModerationFlagging_FunctionsCorrectly()
    {
        var eventId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var thread = MessageThread.Create(ThreadContextType.Event, eventId, "Event Coordination", authorId);

        Assert.Equal(ThreadContextType.Event, thread.ContextType);
        Assert.False(thread.IsClosed);

        var msgResult = ThreadMessage.Create(thread.Id, authorId, "Ich bringe Kuchen mit!");
        Assert.True(msgResult.IsSuccess);
        var msg = msgResult.Value!;
        Assert.False(msg.IsFlaggedForModeration);

        msg.FlagForModeration("Spam / inappropriate advertising");
        Assert.True(msg.IsFlaggedForModeration);
        Assert.Equal("Spam / inappropriate advertising", msg.ModerationReason);
    }
}
