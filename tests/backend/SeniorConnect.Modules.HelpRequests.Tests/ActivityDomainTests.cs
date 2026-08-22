using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class ActivityDomainTests
{
    private static ActivityCategory CreateNormalCategory()
    {
        // Use reflection to create instance because private constructor
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Id))!.SetValue(category, Guid.NewGuid());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, "VISIT");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, "activity.visit");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsBlocked))!.SetValue(category, false);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(category, true);
        return category;
    }

    private static ActivityCategory CreateBlockedCategory()
    {
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Id))!.SetValue(category, Guid.NewGuid());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, "MEDICAL");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, "activity.medical");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsBlocked))!.SetValue(category, true);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.ReferralGroup))!.SetValue(category, "care_services");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(category, true);
        return category;
    }

    [Fact]
    public void Log_WithValidInputs_Succeeds()
    {
        var volunteerId = Guid.NewGuid();
        var category = CreateNormalCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: today,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActivityStatus.Logged, result.Value!.Status);
        Assert.Equal(60, result.Value.DurationMinutes);
    }

    [Fact]
    public void Log_WithBlockedCategory_FailsWithCategoryBlockedError()
    {
        var volunteerId = Guid.NewGuid();
        var category = CreateBlockedCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: today,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        Assert.True(result.IsFailure);
        Assert.Equal("CATEGORY_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public void Log_WithFutureDate_Fails()
    {
        var volunteerId = Guid.NewGuid();
        var category = CreateNormalCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        var result = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: tomorrow,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public void Confirm_BySelf_FailsWithAntiSelfConfirmationRule()
    {
        var volunteerId = Guid.NewGuid();
        var category = CreateNormalCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var logResult = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: today,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        var activity = logResult.Value!;
        var confirmResult = activity.Confirm(volunteerId);

        Assert.True(confirmResult.IsFailure);
        Assert.Equal("SELF_CONFIRMATION_FORBIDDEN", confirmResult.Error!.Code);
    }

    [Fact]
    public void Confirm_ByCoordinator_SucceedsAndEmitsEvent()
    {
        var volunteerId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var category = CreateNormalCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var logResult = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: today,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        var activity = logResult.Value!;
        var confirmResult = activity.Confirm(coordinatorId);

        Assert.True(confirmResult.IsSuccess);
        Assert.Equal(ActivityStatus.Confirmed, activity.Status);
        Assert.Equal(coordinatorId, activity.ConfirmedByUserId);
        Assert.NotEmpty(activity.DomainEvents);
    }

    [Fact]
    public void Dispute_WithValidReason_TransitionsToDisputed()
    {
        var volunteerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var category = CreateNormalCategory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var logResult = Activity.Log(
            organizationId: null,
            volunteerUserId: volunteerId,
            subjectUserId: null,
            category: category,
            occurredOn: today,
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            transportMode: TransportMode.None,
            insuranceContext: InsuranceContext.PrivateNeighbourly,
            source: ActivitySource.SelfLogged,
            loggedByUserId: volunteerId,
            today: today);

        var activity = logResult.Value!;
        var disputeResult = activity.Dispute(otherUserId, "Hours do not match attendance");

        Assert.True(disputeResult.IsSuccess);
        Assert.Equal(ActivityStatus.Disputed, activity.Status);
    }
}
