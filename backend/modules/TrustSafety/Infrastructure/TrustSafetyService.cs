using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class TrustSafetyService : ITrustSafetyService
{
    private readonly ITrustSafetyDbContext _db;
    private readonly IOrganizationCoordinatorReader _orgReader;
    private readonly IAssignmentBlockEnforcementPort? _assignmentEnforcement;

    public TrustSafetyService(ITrustSafetyDbContext db, IOrganizationCoordinatorReader orgReader,
        IAssignmentBlockEnforcementPort? assignmentEnforcement = null)
    {
        _db = db;
        _orgReader = orgReader;
        _assignmentEnforcement = assignmentEnforcement;
    }

    public async Task<Result<BuddyStatusDto>> GetBuddyStatusAsync(
        Guid volunteerUserId,
        Guid viewerUserId,
        bool viewerIsSafeguardingOfficer = false,
        CancellationToken cancellationToken = default)
    {
        // BR-SAFETY-05: buddy status is a safety fact about a named person, and
        // a waiver carries the coordinator's written reason — typically why that
        // person was judged safe to work alone. It must not be readable by any
        // authenticated caller, and a non-officer must not learn from the
        // response whether the volunteer exists or is waived.
        if (viewerUserId != volunteerUserId && !viewerIsSafeguardingOfficer)
        {
            var volunteerOrgIds = await _db.VolunteerApplications
                .Where(a => a.UserId == volunteerUserId
                         && a.Status == ApplicationStatus.Approved
                         && a.OrganizationId != null)
                .Select(a => a.OrganizationId!.Value)
                .ToListAsync(cancellationToken);

            if (!await IsCoordinatorOfAnyAsync(viewerUserId, volunteerOrgIds, cancellationToken))
            {
                return new Error(
                    "BUDDY_STATUS_NOT_FOUND",
                    "Buddy status is not available.",
                    ErrorKind.NotFound);
            }
        }

        var buddy = await _db.BuddyAssignments
            .FirstOrDefaultAsync(b => b.VolunteerUserId == volunteerUserId, cancellationToken);

        // The waiver justification is an officer/coordinator-level record. A
        // volunteer looking at their own status sees the decision, not the
        // internal reason attached to it.
        var mayReadWaiverReason = viewerIsSafeguardingOfficer || viewerUserId != volunteerUserId;

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
            WaiverReason: mayReadWaiverReason ? buddy.WaiverReason : null));
    }

    public async Task<Result<BuddyStatusDto>> WaiveBuddyAsync(
        Guid volunteerUserId,
        Guid actorUserId,
        bool actorIsSafeguardingOfficer,
        WaiveBuddyRequest request,
        CancellationToken cancellationToken = default)
    {
        // BR-SAFETY-05: only a coordinator of the volunteer's organisation (or a safeguarding
        // officer) may waive. The self-waiver case is rejected by the domain below.
        if (actorUserId != volunteerUserId && !actorIsSafeguardingOfficer)
        {
            var volunteerOrgIds = await _db.VolunteerApplications
                .Where(a => a.UserId == volunteerUserId
                         && a.Status == ApplicationStatus.Approved
                         && a.OrganizationId != null)
                .Select(a => a.OrganizationId!.Value)
                .ToListAsync(cancellationToken);

            if (!await IsCoordinatorOfAnyAsync(actorUserId, volunteerOrgIds, cancellationToken))
            {
                return new Error(
                    "BUDDY_WAIVER_COORDINATOR_REQUIRED",
                    "Only a coordinator of the volunteer's organisation or a safeguarding officer can waive the buddy requirement.",
                    ErrorKind.Forbidden);
            }
        }

        var buddy = await _db.BuddyAssignments
            .FirstOrDefaultAsync(b => b.VolunteerUserId == volunteerUserId, cancellationToken);

        if (buddy is null)
        {
            buddy = BuddyAssignment.Create(volunteerUserId);
            _db.BuddyAssignments.Add(buddy);
        }

        var waiveResult = buddy.Waive(actorUserId, request.Reason);
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
        Guid actorUserId,
        HandoverKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsKeyPartyOrCoordinatorAsync(
                actorUserId, request.SeniorUserId, request.VolunteerUserId, request.OrganizationId, cancellationToken))
        {
            return new Error(
                "KEY_CUSTODY_PARTY_REQUIRED",
                "Only the senior, the volunteer or a coordinator of the organisation can record a key handover.",
                ErrorKind.Forbidden);
        }

        var createResult = KeyCustody.Create(
            seniorUserId: request.SeniorUserId,
            volunteerUserId: request.VolunteerUserId,
            keyTag: request.KeyTag,
            organizationId: request.OrganizationId,
            expectedReturnAtUtc: request.ExpectedReturnAtUtc,
            notes: request.Notes,
            handedOverByUserId: actorUserId);

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
        Guid actorUserId,
        ReturnKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = await _db.KeyCustodies
            .FirstOrDefaultAsync(k => k.Id == keyCustodyId, cancellationToken);

        // Outsiders get 404, never 403: a 403 would confirm the record exists.
        if (key is null
            || !await IsKeyPartyOrCoordinatorAsync(
                actorUserId, key.SeniorUserId, key.VolunteerUserId, key.OrganizationId, cancellationToken))
        {
            return Error.NotFound("KeyCustody");
        }

        var returnResult = key.Return(actorUserId, request.Notes);
        if (returnResult.IsFailure)
        {
            return returnResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<KeyCustodyDto>.Success(MapKey(key));
    }

    public async Task<Result<ExpenseRecordDto>> RecordExpenseAsync(
        Guid actorUserId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId != request.SeniorUserId && actorUserId != request.VolunteerUserId)
        {
            return new Error(
                "EXPENSE_PARTY_REQUIRED",
                "Only the senior or the volunteer involved can record an expense.",
                ErrorKind.Forbidden);
        }

        var createResult = ExpenseRecord.Create(
            seniorUserId: request.SeniorUserId,
            volunteerUserId: request.VolunteerUserId,
            amountGiven: request.AmountGiven,
            amountSpent: request.AmountSpent,
            amountReturned: request.AmountReturned,
            receiptNotes: request.ReceiptNotes,
            activityId: request.ActivityId,
            currency: request.Currency,
            recordedByUserId: actorUserId);

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
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var expense = await FindExpenseForPartyAsync(expenseId, actorUserId, cancellationToken);
        if (expense is null)
        {
            return Error.NotFound("ExpenseRecord");
        }

        var confirmResult = expense.Confirm(actorUserId);
        if (confirmResult.IsFailure)
        {
            return confirmResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ExpenseRecordDto>.Success(MapExpense(expense));
    }

    public async Task<Result<ExpenseRecordDto>> DisputeExpenseAsync(
        Guid expenseId,
        Guid actorUserId,
        DisputeExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        // BR-EXPENSE-04: either party may dispute.
        var expense = await FindExpenseForPartyAsync(expenseId, actorUserId, cancellationToken);
        if (expense is null)
        {
            return Error.NotFound("ExpenseRecord");
        }

        var disputeResult = expense.Dispute(actorUserId, request.Reason);
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

        // Phase 4: blocking twice is idempotent (unique pair index).
        var block = await _db.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockingUserId == blockingUserId && b.BlockedUserId == request.BlockedUserId, cancellationToken);
        if (block is null)
        {
            block = UserBlock.Create(blockingUserId, request.BlockedUserId, request.Reason);
            _db.UserBlocks.Add(block);
            await _db.SaveChangesAsync(cancellationToken);

            // B-1 (Option B, approved 2026-09-30): a block between participants
            // releases their live assignment(s) back to the open pool. Only on
            // FIRST creation (idempotent — a repeat block does nothing). The
            // counterpart is never told why (BR-DISC-05); the request simply
            // re-enters matching.
            if (_assignmentEnforcement is { } enforcement)
            {
                await enforcement.UnassignLiveAssignmentsBetweenAsync(
                    blockingUserId, request.BlockedUserId, cancellationToken);
            }
        }

        return Result<UserBlockDto>.Success(new UserBlockDto(
            Id: block.Id,
            BlockingUserId: block.BlockingUserId,
            BlockedUserId: block.BlockedUserId,
            Reason: block.Reason,
            CreatedAtUtc: block.CreatedAtUtc));
    }

    /// <summary>
    /// Phase 4: only the person who blocked can lift their own block. A block
    /// the other person created is not theirs to remove, and its existence is
    /// not confirmed (404 either way).
    /// </summary>
    public async Task<Result> UnblockUserAsync(
        Guid blockingUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        var block = await _db.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockingUserId == blockingUserId && b.BlockedUserId == blockedUserId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("Block");
        }
        _db.UserBlocks.Remove(block);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MyBlockDto>>> GetMyBlocksAsync(
        Guid blockingUserId,
        CancellationToken cancellationToken = default)
    {
        var blocks = await _db.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockingUserId == blockingUserId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new MyBlockDto(b.BlockedUserId, b.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<MyBlockDto>>.Success(blocks);
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

    /// <summary>Only the two parties can see an expense record; everyone else gets 404.</summary>
    private Task<ExpenseRecord?> FindExpenseForPartyAsync(Guid expenseId, Guid actorUserId, CancellationToken ct) =>
        _db.ExpenseRecords.FirstOrDefaultAsync(
            e => e.Id == expenseId && (e.SeniorUserId == actorUserId || e.VolunteerUserId == actorUserId), ct);

    private async Task<bool> IsKeyPartyOrCoordinatorAsync(
        Guid actorUserId, Guid seniorUserId, Guid volunteerUserId, Guid? organizationId, CancellationToken ct)
    {
        if (actorUserId == seniorUserId || actorUserId == volunteerUserId)
        {
            return true;
        }

        return organizationId.HasValue
            && await IsCoordinatorOfAnyAsync(actorUserId, [organizationId.Value], ct);
    }

    private async Task<bool> IsCoordinatorOfAnyAsync(Guid userId, List<Guid> organizationIds, CancellationToken ct)
    {
        if (organizationIds.Count == 0)
        {
            return false;
        }

        var staffOrgs = await _orgReader.GetActiveMembershipsForUserAsync(userId, ct);
        return staffOrgs.Any(o => organizationIds.Contains(o.OrganizationId));
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
