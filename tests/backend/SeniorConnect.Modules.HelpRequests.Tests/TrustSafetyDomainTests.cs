using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class TrustSafetyDomainTests
{
    [Fact]
    public void BuddyAssignment_First3Activities_RequiresBuddyUntil3rdCompleted()
    {
        var volunteerId = Guid.NewGuid();
        var buddy = BuddyAssignment.Create(volunteerId);

        Assert.True(buddy.IsBuddyRequired);
        Assert.Equal(0, buddy.Level3PlusCompletedCount);

        buddy.RecordActivityCompleted();
        Assert.True(buddy.IsBuddyRequired);
        Assert.Equal(1, buddy.Level3PlusCompletedCount);

        buddy.RecordActivityCompleted();
        Assert.True(buddy.IsBuddyRequired);

        buddy.RecordActivityCompleted();
        Assert.False(buddy.IsBuddyRequired);
        Assert.Equal(3, buddy.Level3PlusCompletedCount);
    }

    [Fact]
    public void BuddyAssignment_CoordinatorWaiver_RelievesBuddyRequirement()
    {
        var volunteerId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var buddy = BuddyAssignment.Create(volunteerId);

        var waiveResult = buddy.Waive(coordinatorId, "Experienced former Red Cross volunteer");

        Assert.True(waiveResult.IsSuccess);
        Assert.False(buddy.IsBuddyRequired);
        Assert.True(buddy.IsWaived);
        Assert.Equal("Experienced former Red Cross volunteer", buddy.WaiverReason);
    }

    [Fact]
    public void ExpenseRecord_MathDiscrepancy_FailsValidation()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        // 50 given - 30 spent = 20 should be returned, but 15 provided
        var result = ExpenseRecord.Create(
            seniorUserId: seniorId,
            volunteerUserId: volunteerId,
            amountGiven: 50.0m,
            amountSpent: 30.0m,
            amountReturned: 15.0m);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public void ExpenseRecord_ValidMath_SucceedsAndCanBeConfirmedOrDisputed()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        var result = ExpenseRecord.Create(
            seniorUserId: seniorId,
            volunteerUserId: volunteerId,
            amountGiven: 50.0m,
            amountSpent: 30.0m,
            amountReturned: 20.0m,
            receiptNotes: "Supermarket groceries");

        Assert.True(result.IsSuccess);
        var expense = result.Value!;
        Assert.Equal(ExpenseStatus.Draft, expense.Status);

        var confirmResult = expense.Confirm();
        Assert.True(confirmResult.IsSuccess);
        Assert.Equal(ExpenseStatus.Confirmed, expense.Status);
    }

    [Fact]
    public void KeyCustody_HandoverAndReturn_Succeeds()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        var handoverResult = KeyCustody.Create(
            seniorUserId: seniorId,
            volunteerUserId: volunteerId,
            keyTag: "Front door key",
            expectedReturnAtUtc: DateTimeOffset.UtcNow.AddHours(3));

        Assert.True(handoverResult.IsSuccess);
        var key = handoverResult.Value!;
        Assert.Equal(KeyCustodyStatus.Held, key.Status);

        var returnResult = key.Return("Returned safely to key safe");
        Assert.True(returnResult.IsSuccess);
        Assert.Equal(KeyCustodyStatus.Returned, key.Status);
        Assert.NotNull(key.ReturnedAtUtc);
    }

    [Fact]
    public void SafeguardingCase_RaiseAndCloseWorkflow_Succeeds()
    {
        var reporterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        var raiseResult = SafeguardingCase.Raise(
            reporterUserId: reporterId,
            subjectUserId: subjectId,
            summary: "Volunteer reported signs of cognitive deterioration and confusion",
            severity: SafeguardingSeverity.High,
            category: "welfare_concern");

        Assert.True(raiseResult.IsSuccess);
        var sCase = raiseResult.Value!;
        Assert.Equal(SafeguardingStatus.Open, sCase.Status);
        Assert.Equal(SafeguardingSeverity.High, sCase.Severity);

        var assignResult = sCase.AssignToOfficer(officerId);
        Assert.True(assignResult.IsSuccess);
        Assert.Equal(SafeguardingStatus.Assigned, sCase.Status);
        Assert.Equal(officerId, sCase.AssignedOfficerUserId);

        var closeResult = sCase.Close(officerId, "Contacted family caregiver and local social services");
        Assert.True(closeResult.IsSuccess);
        Assert.Equal(SafeguardingStatus.Closed, sCase.Status);
        Assert.NotNull(sCase.ResolvedAtUtc);
    }
}
