using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class GroupManagementTests
{
    [Fact]
    public void Group_creation_by_individual_succeeds_without_organization()
    {
        // ADR-025 / BR-COMM-06 (amends ADR-020): individual users — CreatorType
        // .Individual or .Volunteer — may create a community group without an
        // organization. The server-side eligibility gate (account active,
        // trust level, no open safeguarding case) lives in CommunityService,
        // not this domain-level check. Supersedes the old
        // "Group_creation_without_an_organization_is_rejected" test.
        var creatorUserId = Guid.NewGuid();

        var groupResult = CommunityGroup.Create(
            creatorUserId: creatorUserId,
            creatorType: CreatorType.Individual,
            name: "Wiener Senioren Schachklub",
            description: "Wöchentliches Schachtreffen im Kaffeehaus.",
            type: GroupType.InterestGroup,
            visibility: GroupVisibility.Public,
            organizationId: null,
            postalCode: "1010");

        groupResult.IsSuccess.Should().BeTrue();
        var group = groupResult.Value!;
        group.CreatorUserId.Should().Be(creatorUserId);
        group.OrganizationId.Should().BeNull();
        group.Name.Should().Be("Wiener Senioren Schachklub");
    }

    [Fact]
    public void Group_creation_by_organization_role_without_organization_is_rejected()
    {
        // The remaining real rule: org-role creator types (OrganizationStaff,
        // VerifiedOrganization) still require an organizationId — see
        // Error.OrganizationRequiredForGroup() in CommunityGroup.cs.
        var groupResult = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            creatorType: CreatorType.OrganizationStaff,
            name: "Wiener Senioren Schachklub",
            description: "Wöchentliches Schachtreffen im Kaffeehaus.",
            type: GroupType.InterestGroup,
            visibility: GroupVisibility.Public,
            organizationId: null,
            postalCode: "1010");

        groupResult.IsFailure.Should().BeTrue();
        groupResult.Error!.Code.Should().Be("ORGANIZATION_REQUIRED");
    }

    [Fact]
    public void Group_can_be_created_by_an_organization()
    {
        var creatorUserId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var groupResult = CommunityGroup.Create(
            creatorUserId: creatorUserId,
            creatorType: CreatorType.VerifiedOrganization,
            name: "Wiener Senioren Schachklub",
            description: "Wöchentliches Schachtreffen im Kaffeehaus.",
            type: GroupType.InterestGroup,
            visibility: GroupVisibility.Public,
            organizationId: organizationId,
            postalCode: "1010");

        groupResult.IsSuccess.Should().BeTrue();
        var group = groupResult.Value!;
        group.CreatorUserId.Should().Be(creatorUserId);
        group.OrganizationId.Should().Be(organizationId);
        group.Name.Should().Be("Wiener Senioren Schachklub");
        group.Category.Should().Be("interest_group");
        group.Visibility.Should().Be(GroupVisibility.Public);
    }

    [Fact]
    public void Creating_group_with_empty_name_fails_validation()
    {
        var groupResult = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            creatorType: CreatorType.Individual,
            name: "   ",
            description: "Description",
            type: GroupType.InterestGroup);

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
        pendingMembership.Status.Should().Be(GroupMembershipStatus.Pending);

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
