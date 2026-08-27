using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class TrustSafetyService : ITrustSafetyService
{
    private readonly ITrustSafetyDbContext _db;

    public TrustSafetyService(ITrustSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<Result<BuddyStatusDto>> GetBuddyStatusAsync(
        Guid volunteerUserId,
        CancellationToken cancellationToken = default)
    {
        var buddy = await _db.BuddyAssignments
            .FirstOrDefaultAsync(b => b.VolunteerUserId == volunteerUserId, cancellationToken);

        if (buddy is null)
        {
            return Result<BuddyStatusDto>.Success(new BuddyStatusDto(
                VolunteerUserId: volunteerUserId,
                Level3PlusCompletedCount: 0,
                IsBuddyRequired: true,
                AssignedBuddyVolunteerUserId: null,
                IsWaived: false,
                WaiverReason: null));
        }

        return Result<BuddyStatusDto>.Success(new BuddyStatusDto(
            VolunteerUserId: buddy.VolunteerUserId,
            Level3PlusCompletedCount: buddy.Level3PlusCompletedCount,
            IsBuddyRequired: buddy.IsBuddyRequired,
            AssignedBuddyVolunteerUserId: buddy.AssignedBuddyVolunteerUserId,
            IsWaived: buddy.IsWaived,
            WaiverReason: buddy.WaiverReason));
    }

    public async Task<Result<BuddyStatusDto>> WaiveBuddyAsync(
        Guid volunteerUserId,
        Guid coordinatorUserId,
        WaiveBuddyRequest request,
        CancellationToken cancellationToken = default)
    {
        var buddy = await _db.BuddyAssignments
            .FirstOrDefaultAsync(b => b.VolunteerUserId == volunteerUserId, cancellationToken);

        if (buddy is null)
        {
            buddy = BuddyAssignment.Create(volunteerUserId);
            _db.BuddyAssignments.Add(buddy);
        }

        var waiveResult = buddy.Waive(coordinatorUserId, request.Reason);
        if (waiveResult.IsFailure)
        {
            return waiveResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<BuddyStatusDto>.Success(new BuddyStatusDto(
            VolunteerUserId: buddy.VolunteerUserId,
            Level3PlusCompletedCount: buddy.Level3PlusCompletedCount,
            IsBuddyRequired: false,
            AssignedBuddyVolunteerUserId: buddy.AssignedBuddyVolunteerUserId,
            IsWaived: true,
            WaiverReason: buddy.WaiverReason));
    }

    private static readonly string[] BeforeChecklist =
    [
        "protocol.before.confirm_name_and_time",
        "protocol.before.agree_meeting_place",
        "protocol.before.notify_trusted_person"
    ];

    private static readonly string[] DuringChecklist =
    [
        "protocol.during.check_in_app"
    ];

    private static readonly string[] AfterChecklist =
    [
        "protocol.after.check_out_app",
        "protocol.after.confirm_all_in_order_or_report"
    ];

    public Task<Result<FirstMeetingProtocolDto>> GetFirstMeetingProtocolAsync(
        CancellationToken cancellationToken = default)
    {
        var protocol = new FirstMeetingProtocolDto(
            BeforeMeetingChecklist: BeforeChecklist,
            DuringMeetingChecklist: DuringChecklist,
            AfterMeetingChecklist: AfterChecklist);

        return Task.FromResult(Result<FirstMeetingProtocolDto>.Success(protocol));
    }

    public async Task<Result<KeyCustodyDto>> HandoverKeyAsync(
        HandoverKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var createResult = KeyCustody.Create(
            seniorUserId: request.SeniorUserId,
            volunteerUserId: request.VolunteerUserId,
            keyTag: request.KeyTag,
            organizationId: request.OrganizationId,
            expectedReturnAtUtc: request.ExpectedReturnAtUtc,
            notes: request.Notes);

        if (createResult.IsFailure)
        {
            return createResult.Error!;
        }

        var key = createResult.Value!;
        _db.KeyCustodies.Add(key);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<KeyCustodyDto>.Success(MapKey(key));
    }

    public async Task<Result<KeyCustodyDto>> ReturnKeyAsync(
        Guid keyCustodyId,
        ReturnKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = await _db.KeyCustodies
            .FirstOrDefaultAsync(k => k.Id == keyCustodyId, cancellationToken);

        if (key is null)
        {
            return Error.NotFound("KeyCustody");
        }

        var returnResult = key.Return(request.Notes);
        if (returnResult.IsFailure)
        {
            return returnResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<KeyCustodyDto>.Success(MapKey(key));
    }

    public async Task<Result<ExpenseRecordDto>> RecordExpenseAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var createResult = ExpenseRecord.Create(
            seniorUserId: request.SeniorUserId,
            volunteerUserId: request.VolunteerUserId,
            amountGiven: request.AmountGiven,
            amountSpent: request.AmountSpent,
            amountReturned: request.AmountReturned,
            receiptNotes: request.ReceiptNotes,
            activityId: request.ActivityId,
            currency: request.Currency);

        if (createResult.IsFailure)
        {
            return createResult.Error!;
        }

        var expense = createResult.Value!;
        _db.ExpenseRecords.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ExpenseRecordDto>.Success(MapExpense(expense));
    }

    public async Task<Result<ExpenseRecordDto>> ConfirmExpenseAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        var expense = await _db.ExpenseRecords
            .FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);

        if (expense is null)
        {
            return Error.NotFound("ExpenseRecord");
        }

        var confirmResult = expense.Confirm();
        if (confirmResult.IsFailure)
        {
            return confirmResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ExpenseRecordDto>.Success(MapExpense(expense));
    }

    public async Task<Result<ExpenseRecordDto>> DisputeExpenseAsync(
        Guid expenseId,
        DisputeExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var expense = await _db.ExpenseRecords
            .FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);

        if (expense is null)
        {
            return Error.NotFound("ExpenseRecord");
        }

        var disputeResult = expense.Dispute(request.Reason);
        if (disputeResult.IsFailure)
        {
            return disputeResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ExpenseRecordDto>.Success(MapExpense(expense));
    }

    public async Task<Result<UserBlockDto>> BlockUserAsync(
        Guid blockingUserId,
        CreateBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (blockingUserId == request.BlockedUserId)
        {
            return Error.Validation("You cannot block yourself.");
        }

        var block = UserBlock.Create(blockingUserId, request.BlockedUserId, request.Reason);
        _db.UserBlocks.Add(block);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserBlockDto>.Success(new UserBlockDto(
            Id: block.Id,
            BlockingUserId: block.BlockingUserId,
            BlockedUserId: block.BlockedUserId,
            Reason: block.Reason,
            CreatedAtUtc: block.CreatedAtUtc));
    }

    public async Task<bool> IsBlockedAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserBlocks
            .AnyAsync(b => (b.BlockingUserId == userA && b.BlockedUserId == userB) ||
                           (b.BlockingUserId == userB && b.BlockedUserId == userA), cancellationToken);
    }

    private static KeyCustodyDto MapKey(KeyCustody k) => new(
        Id: k.Id,
        SeniorUserId: k.SeniorUserId,
        VolunteerUserId: k.VolunteerUserId,
        OrganizationId: k.OrganizationId,
        KeyTag: k.KeyTag,
        HandedOverAtUtc: k.HandedOverAtUtc,
        ExpectedReturnAtUtc: k.ExpectedReturnAtUtc,
        ReturnedAtUtc: k.ReturnedAtUtc,
        Status: k.Status,
        Notes: k.Notes);

    private static ExpenseRecordDto MapExpense(ExpenseRecord e) => new(
        Id: e.Id,
        ActivityId: e.ActivityId,
        SeniorUserId: e.SeniorUserId,
        VolunteerUserId: e.VolunteerUserId,
        AmountGiven: e.AmountGiven,
        AmountSpent: e.AmountSpent,
        AmountReturned: e.AmountReturned,
        Currency: e.Currency,
        ReceiptNotes: e.ReceiptNotes,
        Status: e.Status,
        DisputeReason: e.DisputeReason,
        CreatedAtUtc: e.CreatedAtUtc);
}
