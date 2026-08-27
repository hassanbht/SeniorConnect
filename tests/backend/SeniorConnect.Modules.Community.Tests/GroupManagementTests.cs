using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class GroupManagementTests
{
    [Fact]
    public void Group_can_be_created_without_an_organization()
    {
        var creatorUserId = Guid.NewGuid();

        var groupResult = CommunityGroup.Create(
            creatorUserId: creatorUserId,
            title: "Wiener Senioren Schachklub",
            description: "Wöchentliches Schachtreffen im Kaffeehaus.",
            category: "sports",
            scope: GroupVisibilityScope.Public,
            joinPolicy: GroupJoinPolicy.Open,
            organizationId: null, // Zero organization requirement (P5-01 / Gate 5)
            locationPostalCode: "1010");

        groupResult.IsSuccess.Should().BeTrue();
        var group = groupResult.Value!;
        group.CreatorUserId.Should().Be(creatorUserId);
        group.OrganizationId.Should().BeNull();
        group.Title.Should().Be("Wiener Senioren Schachklub");
        group.Category.Should().Be("sports");
        group.JoinPolicy.Should().Be(GroupJoinPolicy.Open);
        group.Scope.Should().Be(GroupVisibilityScope.Public);
    }

    [Fact]
    public void Creating_group_with_empty_title_fails_validation()
    {
        var groupResult = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            title: "   ",
            description: "Description");

        groupResult.IsFailure.Should().BeTrue();
        groupResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Open_group_allows_direct_active_membership()
    {
        var groupId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();

        var membership = GroupMembership.JoinDirectly(groupId, memberUserId);

        membership.GroupId.Should().Be(groupId);
        membership.UserId.Should().Be(memberUserId);
        membership.Status.Should().Be(GroupMembershipStatus.Active);
        membership.Role.Should().Be(GroupMemberRole.Member);
    }

    [Fact]
    public void Request_approval_group_creates_pending_membership_and_can_be_approved()
    {
        var groupId = Guid.NewGuid();
        var applicantUserId = Guid.NewGuid();
        var approverUserId = Guid.NewGuid();

        var pendingMembership = GroupMembership.RequestToJoin(groupId, applicantUserId);
        pendingMembership.Status.Should().Be(GroupMembershipStatus.PendingApproval);

        var approveResult = pendingMembership.Approve(approverUserId);
        approveResult.IsSuccess.Should().BeTrue();
        pendingMembership.Status.Should().Be(GroupMembershipStatus.Active);
        pendingMembership.UpdatedBy.Should().Be(approverUserId);
    }

    [Fact]
    public void Pending_membership_can_be_rejected_by_admin()
    {
        var groupId = Guid.NewGuid();
        var applicantUserId = Guid.NewGuid();
        var rejectorUserId = Guid.NewGuid();

        var pendingMembership = GroupMembership.RequestToJoin(groupId, applicantUserId);
        var rejectResult = pendingMembership.Reject(rejectorUserId);

        rejectResult.IsSuccess.Should().BeTrue();
        pendingMembership.Status.Should().Be(GroupMembershipStatus.Rejected);
        pendingMembership.UpdatedBy.Should().Be(rejectorUserId);
    }
}
