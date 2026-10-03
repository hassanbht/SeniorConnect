using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface ITrustSafetyService
{
    Task<Result<BuddyStatusDto>> GetBuddyStatusAsync(
        Guid volunteerUserId,
        CancellationToken cancellationToken = default);

    /// <param name="actorIsSafeguardingOfficer">Computed server-side from the caller's claims.</param>
    Task<Result<BuddyStatusDto>> WaiveBuddyAsync(
        Guid volunteerUserId,
        Guid actorUserId,
        bool actorIsSafeguardingOfficer,
        WaiveBuddyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<FirstMeetingProtocolDto>> GetFirstMeetingProtocolAsync(
        CancellationToken cancellationToken = default);

    Task<Result<KeyCustodyDto>> HandoverKeyAsync(
        Guid actorUserId,
        HandoverKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<KeyCustodyDto>> ReturnKeyAsync(
        Guid keyCustodyId,
        Guid actorUserId,
        ReturnKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> RecordExpenseAsync(
        Guid actorUserId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> ConfirmExpenseAsync(
        Guid expenseId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> DisputeExpenseAsync(
        Guid expenseId,
        Guid actorUserId,
        DisputeExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserBlockDto>> BlockUserAsync(
        Guid blockingUserId,
        CreateBlockRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> UnblockUserAsync(
        Guid blockingUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MyBlockDto>>> GetMyBlocksAsync(
        Guid blockingUserId,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default);
}

public interface ISafeguardingService
{
    Task<Result<SafeguardingCaseDto>> RaiseConcernAsync(
        Guid reporterUserId,
        RaiseConcernRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SafeguardingCaseDto>> GetCaseByIdAsync(
        Guid caseId,
        Guid officerUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SafeguardingCaseDto>>> GetCasesAsync(
        Guid? organizationId,
        Guid officerUserId,
        CancellationToken cancellationToken = default);

    Task<Result> AddCaseNoteAsync(
        Guid caseId,
        Guid officerUserId,
        AddCaseNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SafeguardingCaseDto>> AssignCaseAsync(
        Guid caseId,
        Guid officerUserId,
        AssignCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SafeguardingRestrictionDto>> ImposeRestrictionAsync(
        Guid caseId,
        Guid officerUserId,
        ImposeRestrictionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SafeguardingRestrictionDto>> LiftRestrictionAsync(
        Guid restrictionId,
        Guid officerUserId,
        LiftRestrictionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SafeguardingRestrictionDto>>> GetRestrictionsAsync(
        Guid caseId,
        Guid officerUserId,
        CancellationToken cancellationToken = default);

    Task<Result<SafeguardingCaseDto>> CloseCaseAsync(
        Guid caseId,
        Guid officerUserId,
        CloseCaseRequest request,
        CancellationToken cancellationToken = default);
}
