using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

public sealed class BuddyAssignment : Entity, IAuditable
{
    private BuddyAssignment() { }

    [DataClass(DataClass.Operational)]
    public Guid VolunteerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public int Level3PlusCompletedCount { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsBuddyRequired { get; private set; } = true;

    [DataClass(DataClass.Operational)]
    public Guid? AssignedBuddyVolunteerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsWaived { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? WaiverReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? WaivedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? WaivedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static BuddyAssignment Create(Guid volunteerUserId)
    {
        var now = DateTimeOffset.UtcNow;
        return new BuddyAssignment
        {
            Id = Guid.CreateVersion7(),
            VolunteerUserId = volunteerUserId,
            Level3PlusCompletedCount = 0,
            IsBuddyRequired = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void RecordActivityCompleted()
    {
        Level3PlusCompletedCount++;
        if (Level3PlusCompletedCount >= 3)
        {
            IsBuddyRequired = false;
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Result Waive(Guid coordinatorUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Waiver reason is required.");
        }

        IsWaived = true;
        WaiverReason = reason.Trim();
        WaivedByUserId = coordinatorUserId;
        WaivedAtUtc = DateTimeOffset.UtcNow;
        IsBuddyRequired = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = coordinatorUserId;

        return Result.Success();
    }
}

public enum KeyCustodyStatus
{
    Held,
    Returned,
    Overdue
}

public sealed class KeyCustody : Entity, IAuditable
{
    private KeyCustody() { }

    [DataClass(DataClass.Operational)]
    public Guid SeniorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid VolunteerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string KeyTag { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset HandedOverAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ExpectedReturnAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ReturnedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public KeyCustodyStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Notes { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<KeyCustody> Create(
        Guid seniorUserId,
        Guid volunteerUserId,
        string keyTag,
        Guid? organizationId = null,
        DateTimeOffset? expectedReturnAtUtc = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(keyTag))
        {
            return Error.Validation("Key tag is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<KeyCustody>.Success(new KeyCustody
        {
            Id = Guid.CreateVersion7(),
            SeniorUserId = seniorUserId,
            VolunteerUserId = volunteerUserId,
            KeyTag = keyTag.Trim(),
            OrganizationId = organizationId,
            HandedOverAtUtc = now,
            ExpectedReturnAtUtc = expectedReturnAtUtc,
            Status = KeyCustodyStatus.Held,
            Notes = notes,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    public Result Return(string? notes = null)
    {
        if (Status == KeyCustodyStatus.Returned)
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(KeyCustodyStatus.Returned));
        }

        Status = KeyCustodyStatus.Returned;
        ReturnedAtUtc = DateTimeOffset.UtcNow;
        Notes = notes ?? Notes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }
}

public enum ExpenseStatus
{
    Draft,
    Confirmed,
    Disputed
}

public sealed class ExpenseRecord : Entity, IAuditable
{
    private ExpenseRecord() { }

    [DataClass(DataClass.Operational)]
    public Guid? ActivityId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid SeniorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid VolunteerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public decimal AmountGiven { get; private set; }

    [DataClass(DataClass.Operational)]
    public decimal AmountSpent { get; private set; }

    [DataClass(DataClass.Operational)]
    public decimal AmountReturned { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Currency { get; private set; } = "EUR";

    [DataClass(DataClass.PersonalData)]
    public string? ReceiptNotes { get; private set; }

    [DataClass(DataClass.Operational)]
    public ExpenseStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? DisputeReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<ExpenseRecord> Create(
        Guid seniorUserId,
        Guid volunteerUserId,
        decimal amountGiven,
        decimal amountSpent,
        decimal amountReturned,
        string? receiptNotes = null,
        Guid? activityId = null,
        string currency = "EUR")
    {
        // Validation: Given - Spent must equal Returned
        if (amountGiven - amountSpent != amountReturned)
        {
            return Error.Validation("Math discrepancy: Given amount minus spent amount must equal returned amount.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<ExpenseRecord>.Success(new ExpenseRecord
        {
            Id = Guid.CreateVersion7(),
            ActivityId = activityId,
            SeniorUserId = seniorUserId,
            VolunteerUserId = volunteerUserId,
            AmountGiven = amountGiven,
            AmountSpent = amountSpent,
            AmountReturned = amountReturned,
            Currency = currency,
            ReceiptNotes = receiptNotes,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    public Result Confirm()
    {
        Status = ExpenseStatus.Confirmed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Dispute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Dispute reason is required.");
        }

        Status = ExpenseStatus.Disputed;
        DisputeReason = reason.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}

public sealed class UserBlock : Entity
{
    private UserBlock() { }

    [DataClass(DataClass.Operational)]
    public Guid BlockingUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid BlockedUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Reason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static UserBlock Create(Guid blockingUserId, Guid blockedUserId, string? reason = null)
    {
        return new UserBlock
        {
            Id = Guid.CreateVersion7(),
            BlockingUserId = blockingUserId,
            BlockedUserId = blockedUserId,
            Reason = reason,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
