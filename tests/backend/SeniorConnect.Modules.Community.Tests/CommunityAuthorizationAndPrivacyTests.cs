using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class CommunityAuthorizationAndPrivacyTests
{
    [Fact]
    public void Private_group_has_private_visibility()
    {
        var creatorUserId = Guid.NewGuid();

        var group = CommunityGroup.Create(
            creatorUserId: creatorUserId,
            creatorType: CreatorType.VerifiedOrganization,
            name: "Private Family Caregiver Circle",
            description: "Confidential group",
            type: GroupType.SupportGroup,
            visibility: GroupVisibility.Private,
            organizationId: Guid.NewGuid()).Value!;

        group.Visibility.Should().Be(GroupVisibility.Private);
    }

    [Fact]
    public void Updating_group_name_and_visibility_succeeds_with_valid_input()
    {
        var group = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            creatorType: CreatorType.Individual,
            name: "Old Name",
            description: "Old Desc",
            type: GroupType.InterestGroup).Value!;

        var updateResult = group.Update(
            name: "New Enhanced Name",
            description: "Updated description",
            type: GroupType.InterestGroup,
            visibility: GroupVisibility.Public,
            city: null,
            region: null,
            postalCode: "1020",
            latitude: null,
            longitude: null,
            primaryLanguageCode: null,
            coverImageUrl: null,
            rules: null,
            maxMembers: 20,
            updatedByUserId: group.CreatorUserId);

        updateResult.IsSuccess.Should().BeTrue();
        group.Name.Should().Be("New Enhanced Name");
        group.Visibility.Should().Be(GroupVisibility.Public);
        group.MaxMembers.Should().Be(20);
    }

    [Fact]
    public void Updating_group_with_empty_name_fails()
    {
        var group = CommunityGroup.Create(
            creatorUserId: Guid.NewGuid(),
            creatorType: CreatorType.Individual,
            name: "Valid Name",
            description: "Desc",
            type: GroupType.InterestGroup).Value!;

        var updateResult = group.Update(
            name: "   ",
            description: "Desc",
            type: GroupType.InterestGroup,
            visibility: GroupVisibility.Public,
            city: null,
            region: null,
            postalCode: null,
            latitude: null,
            longitude: null,
            primaryLanguageCode: null,
            coverImageUrl: null,
            rules: null,
            maxMembers: null,
            updatedByUserId: group.CreatorUserId);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }
}
