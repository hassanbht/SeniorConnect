using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class CommunityAuthorizationAndPrivacyTests
{
    [Fact]
    public void Private_group_with_invite_only_policy_cannot_be_joined_directly()
    {
        var creatorUserId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: creatorUserId,
            title: "Private Family Caregiver Circle",
            description: "Confidential group",
            scope: GroupVisibilityScope.PrivateInvitation,
            joinPolicy: GroupJoinPolicy.InviteOnly).Value!;

        group.Scope.Should().Be(GroupVisibilityScope.PrivateInvitation);
        group.JoinPolicy.Should().Be(GroupJoinPolicy.InviteOnly);
    }

    [Fact]
    public void Updating_group_title_and_scope_succeeds_with_valid_input()
    {
        var group = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            title: "Old Title",
            description: "Old Desc").Value!;

        var updateResult = group.Update(
            title: "New Enhanced Title",
            description: "Updated description",
            category: "culture",
            scope: GroupVisibilityScope.LocalNeighborhood,
            joinPolicy: GroupJoinPolicy.RequestApproval,
            locationPostalCode: "1020",
            maxMembers: 20,
            updatedByUserId: group.CreatorUserId);

        updateResult.IsSuccess.Should().BeTrue();
        group.Title.Should().Be("New Enhanced Title");
        group.Scope.Should().Be(GroupVisibilityScope.LocalNeighborhood);
        group.JoinPolicy.Should().Be(GroupJoinPolicy.RequestApproval);
        group.MaxMembers.Should().Be(20);
    }

    [Fact]
    public void Updating_group_with_empty_title_fails()
    {
        var group = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            title: "Valid Title",
            description: "Desc").Value!;

        var updateResult = group.Update(
            title: "   ",
            description: "Desc",
            category: "general",
            scope: GroupVisibilityScope.Public,
            joinPolicy: GroupJoinPolicy.Open,
            locationPostalCode: null,
            maxMembers: null,
            updatedByUserId: group.CreatorUserId);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }
}
