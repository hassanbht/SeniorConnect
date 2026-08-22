using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface ITrustSafetyService
{
    Task<Result<BuddyStatusDto>> GetBuddyStatusAsync(
        Guid volunteerUserId,
        CancellationToken cancellationToken = default);

    Task<Result<BuddyStatusDto>> WaiveBuddyAsync(
        Guid volunteerUserId,
        Guid coordinatorUserId,
        WaiveBuddyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<KeyCustodyDto>> HandoverKeyAsync(
        HandoverKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<KeyCustodyDto>> ReturnKeyAsync(
        Guid keyCustodyId,
        ReturnKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> RecordExpenseAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> ConfirmExpenseAsync(
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseRecordDto>> DisputeExpenseAsync(
        Guid expenseId,
        DisputeExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserBlockDto>> BlockUserAsync(
        Guid blockingUserId,
        CreateBlockRequest request,
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

    Task<Result<SafeguardingCaseDto>> CloseCaseAsync(
        Guid caseId,
        Guid officerUserId,
        CloseCaseRequest request,
        CancellationToken cancellationToken = default);
}
