using FluentAssertions;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class KeyCustodyAndExpenseTests
{
    [Fact]
    public void Key_custody_creates_and_returns_cleanly()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        var createResult = KeyCustody.Create(
            seniorUserId: seniorId,
            volunteerUserId: volunteerId,
            keyTag: "KEY-APARTMENT-4B",
            expectedReturnAtUtc: DateTimeOffset.UtcNow.AddHours(4));

        createResult.IsSuccess.Should().BeTrue();
        var key = createResult.Value!;
        key.Status.Should().Be(KeyCustodyStatus.Held);
        key.KeyTag.Should().Be("KEY-APARTMENT-4B");

        var returnResult = key.Return("Handed back directly after visit.");
        returnResult.IsSuccess.Should().BeTrue();
        key.Status.Should().Be(KeyCustodyStatus.Returned);
        key.ReturnedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Empty_key_tag_fails_validation()
    {
        var createResult = KeyCustody.Create(
            seniorUserId: Guid.NewGuid(),
            volunteerUserId: Guid.NewGuid(),
            keyTag: "   ");

        createResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Expense_creation_succeeds_when_math_is_exact()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        // 50 given, 32.50 spent, 17.50 returned -> 50 - 32.50 == 17.50
        var createResult = ExpenseRecord.Create(
            seniorUserId: seniorId,
            volunteerUserId: volunteerId,
            amountGiven: 50.00m,
            amountSpent: 32.50m,
            amountReturned: 17.50m,
            receiptNotes: "Spar receipt #49281");

        createResult.IsSuccess.Should().BeTrue();
        var expense = createResult.Value!;
        expense.Status.Should().Be(ExpenseStatus.Draft);

        var confirmResult = expense.Confirm();
        confirmResult.IsSuccess.Should().BeTrue();
        expense.Status.Should().Be(ExpenseStatus.Confirmed);
    }

    [Fact]
    public void Expense_creation_fails_when_math_discrepancy_exists()
    {
        // 50 given, 30 spent, 15 returned -> 50 - 30 != 15 (5 EUR unaccounted)
        var createResult = ExpenseRecord.Create(
            seniorUserId: Guid.NewGuid(),
            volunteerUserId: Guid.NewGuid(),
            amountGiven: 50.00m,
            amountSpent: 30.00m,
            amountReturned: 15.00m);

        createResult.IsFailure.Should().BeTrue();
        createResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Expense_can_be_disputed_with_reason()
    {
        var expense = ExpenseRecord.Create(
            seniorUserId: Guid.NewGuid(),
            volunteerUserId: Guid.NewGuid(),
            amountGiven: 20.00m,
            amountSpent: 20.00m,
            amountReturned: 0.00m).Value!;

        var disputeResult = expense.Dispute("Receipt was not provided.");
        disputeResult.IsSuccess.Should().BeTrue();
        expense.Status.Should().Be(ExpenseStatus.Disputed);
        expense.DisputeReason.Should().Be("Receipt was not provided.");
    }
}
