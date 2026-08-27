using FluentAssertions;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class BuddySystemTests
{
    [Fact]
    public void New_volunteer_requires_buddy_for_first_3_level_3_plus_activities()
    {
        var volunteerId = Guid.NewGuid();
        var buddy = BuddyAssignment.Create(volunteerId);

        buddy.IsBuddyRequired.Should().BeTrue();
        buddy.Level3PlusCompletedCount.Should().Be(0);

        // 1st activity
        buddy.RecordActivityCompleted();
        buddy.IsBuddyRequired.Should().BeTrue();
        buddy.Level3PlusCompletedCount.Should().Be(1);

        // 2nd activity
        buddy.RecordActivityCompleted();
        buddy.IsBuddyRequired.Should().BeTrue();
        buddy.Level3PlusCompletedCount.Should().Be(2);

        // 3rd activity -> buddy no longer required
        buddy.RecordActivityCompleted();
        buddy.IsBuddyRequired.Should().BeFalse();
        buddy.Level3PlusCompletedCount.Should().Be(3);
    }

    [Fact]
    public void Coordinator_can_waive_buddy_with_recorded_reason()
    {
        var volunteerId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var buddy = BuddyAssignment.Create(volunteerId);

        var waiveResult = buddy.Waive(coordinatorId, "Experienced certified Red Cross caregiver with 5 years experience.");

        waiveResult.IsSuccess.Should().BeTrue();
        buddy.IsWaived.Should().BeTrue();
        buddy.IsBuddyRequired.Should().BeFalse();
        buddy.WaivedByUserId.Should().Be(coordinatorId);
        buddy.WaiverReason.Should().Contain("Red Cross");
    }

    [Fact]
    public void Waiving_buddy_without_reason_fails_validation()
    {
        var volunteerId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var buddy = BuddyAssignment.Create(volunteerId);

        var waiveResult = buddy.Waive(coordinatorId, "  ");

        waiveResult.IsFailure.Should().BeTrue();
        buddy.IsBuddyRequired.Should().BeTrue();
        buddy.IsWaived.Should().BeFalse();
    }
}
