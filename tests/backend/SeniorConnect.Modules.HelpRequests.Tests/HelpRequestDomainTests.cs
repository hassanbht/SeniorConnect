using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class HelpRequestDomainTests
{
    [Fact]
    public void Create_WithValidData_ReturnsOpenHelpRequest()
    {
        var seniorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var result = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            notes: "Need assistance carrying groceries");

        Assert.True(result.IsSuccess);
        var request = result.Value!;
        Assert.Equal(HelpRequestStatus.Open, request.Status);
        Assert.Equal(60, request.DurationMinutes);
        Assert.Equal(1, request.RowVersion);
    }

    [Fact]
    public void Assign_WithExpectedRowVersion_TransitionsToAssigned()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        var assignResult = request.Assign(volunteerId, expectedRowVersion: 1);

        Assert.True(assignResult.IsSuccess);
        Assert.Equal(HelpRequestStatus.Assigned, request.Status);
        Assert.Equal(volunteerId, request.AssignedVolunteerUserId);
        Assert.Equal(2, request.RowVersion);
    }

    [Fact]
    public void Assign_WithStaleRowVersion_ReturnsConcurrencyConflict()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        // Simulate stale row version
        var assignResult = request.Assign(volunteerId, expectedRowVersion: 99);

        Assert.True(assignResult.IsFailure);
        Assert.Equal("CONCURRENCY_CONFLICT", assignResult.Error!.Code);
    }

    [Fact]
    public void Assign_SeniorToOwnRequest_FailsWithValidationError()
    {
        var seniorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        var assignResult = request.Assign(seniorId, expectedRowVersion: 1);

        Assert.True(assignResult.IsFailure);
        Assert.Equal(ErrorKind.Validation, assignResult.Error!.Kind);
    }

    [Fact]
    public void FullLifecycle_CheckInAndComplete_EmitsHelpRequestCompletedEvent()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        request.Assign(volunteerId, 1);
        var checkInResult = request.CheckIn(volunteerId);
        Assert.True(checkInResult.IsSuccess);
        Assert.Equal(HelpRequestStatus.InProgress, request.Status);

        var completeResult = request.Complete(volunteerId, actualDurationMinutes: 75);
        Assert.True(completeResult.IsSuccess);
        Assert.Equal(HelpRequestStatus.Completed, request.Status);
        Assert.Equal(75, request.DurationMinutes);
        Assert.NotEmpty(request.DomainEvents);

        var domainEvent = Assert.IsType<HelpRequestCompleted>(request.DomainEvents[0]);
        Assert.Equal(volunteerId, domainEvent.VolunteerUserId);
        Assert.Equal(seniorId, domainEvent.SubjectUserId);
    }

    [Fact]
    public void Cancel_WithValidReason_TransitionsToCancelled()
    {
        var seniorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        var cancelResult = request.Cancel(seniorId, "Doctor appointment rescheduled");
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(HelpRequestStatus.Cancelled, request.Status);
        Assert.Equal("Doctor appointment rescheduled", request.CancellationReason);
    }

    [Fact]
    public void EmergencyDetector_IdentifiesEmergencyKeywords()
    {
        Assert.True(EmergencyDetector.IsEmergency("Der Herr klagt über Atemnot"));
        Assert.True(EmergencyDetector.IsEmergency("Bitte 112 rufen, akuter Notfall"));
        Assert.True(EmergencyDetector.IsEmergency("Severe chest pain and unconscious"));
        Assert.False(EmergencyDetector.IsEmergency("Hilfe beim Einkaufen von Obst und Brot"));
    }

    [Fact]
    public void SafetyPolicy_HomeVisitVulnerable_ReturnsLevel5()
    {
        var policy = new ActivitySafetyPolicy();
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;

        var result = policy.Evaluate(
            category: category,
            locationType: LocationType.SeniorHome,
            transportMode: TransportMode.None,
            isSubjectVulnerable: true);

        Assert.Equal(5, result.RequiredSafetyLevel);
        Assert.Equal(5, result.RequiredTrustLevel);
        Assert.True(result.RequiresNamedCoordinator);
        Assert.True(result.RequiresOrganizationApproval);
    }
}
