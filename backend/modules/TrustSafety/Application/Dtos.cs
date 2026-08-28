using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

// --- Phase 2: Onboarding Pipeline DTOs ---
public sealed record VolunteerApplicationDto(
    Guid Id,
    Guid? OrganizationId,
    Guid UserId,
    ApplicationStatus Status,
    string? Motivation,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    Guid? DecidedByUserId,
    string? DeclineReason,
    IReadOnlyList<ApplicationStepDto> Steps);

public sealed record ApplicationStepDto(
    Guid Id,
    Guid ApplicationId,
    ApplicationStepType Step,
    int SortOrder,
    StepStatus Status,
    int SlaDays,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Guid? CompletedByUserId,
    string? Note);

public sealed record ApplyVolunteerRequest(
    Guid OrganizationId,
    string? Motivation = null);

public sealed record UpdateStepRequest(
    StepStatus Status,
    string? Note = null);

public sealed record DecideApplicationRequest(
    bool Approved,
    string? Reason = null);

// --- Phase 4: Buddy System ---
public sealed record BuddyStatusDto(
    Guid VolunteerUserId,
    int Level3PlusCompletedCount,
    bool IsBuddyRequired,
    Guid? AssignedBuddyVolunteerUserId,
    bool IsWaived,
    string? WaiverReason);

public sealed record WaiveBuddyRequest(
    string Reason);

// --- Phase 4: First Meeting Protocol ---
public sealed record FirstMeetingProtocolDto(
    IReadOnlyList<string> BeforeMeetingChecklist,
    IReadOnlyList<string> DuringMeetingChecklist,
    IReadOnlyList<string> AfterMeetingChecklist);

// --- Phase 4: Key Custody ---
public sealed record KeyCustodyDto(
    Guid Id,
    Guid SeniorUserId,
    Guid VolunteerUserId,
    Guid? OrganizationId,
    string KeyTag,
    DateTimeOffset HandedOverAtUtc,
    DateTimeOffset? ExpectedReturnAtUtc,
    DateTimeOffset? ReturnedAtUtc,
    KeyCustodyStatus Status,
    string? Notes);

public sealed record HandoverKeyRequest(
    Guid SeniorUserId,
    Guid VolunteerUserId,
    string KeyTag,
    Guid? OrganizationId = null,
    DateTimeOffset? ExpectedReturnAtUtc = null,
    string? Notes = null);

public sealed record ReturnKeyRequest(
    string? Notes = null);

// --- Phase 4: Expense Records ---
public sealed record ExpenseRecordDto(
    Guid Id,
    Guid? ActivityId,
    Guid SeniorUserId,
    Guid VolunteerUserId,
    decimal AmountGiven,
    decimal AmountSpent,
    decimal AmountReturned,
    string Currency,
    string? ReceiptNotes,
    ExpenseStatus Status,
    string? DisputeReason,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateExpenseRequest(
    Guid SeniorUserId,
    Guid VolunteerUserId,
    decimal AmountGiven,
    decimal AmountSpent,
    decimal AmountReturned,
    string? ReceiptNotes = null,
    Guid? ActivityId = null,
    string Currency = "EUR");

public sealed record DisputeExpenseRequest(
    string Reason);

// --- Phase 4: User Blocking ---
public sealed record UserBlockDto(
    Guid Id,
    Guid BlockingUserId,
    Guid BlockedUserId,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateBlockRequest(
    Guid BlockedUserId,
    string? Reason = null);

// --- Phase 4: Safeguarding ---
public sealed record SafeguardingCaseDto(
    Guid Id,
    Guid? OrganizationId,
    Guid SubjectUserId,
    Guid ReporterUserId,
    SafeguardingSeverity Severity,
    string Category,
    string Summary,
    SafeguardingStatus Status,
    Guid? AssignedOfficerUserId,
    string? ResolutionNotes,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record RaiseConcernRequest(
    Guid SubjectUserId,
    string Summary,
    SafeguardingSeverity Severity = SafeguardingSeverity.Medium,
    string Category = "general_concern",
    Guid? OrganizationId = null);

public sealed record AddCaseNoteRequest(
    string NoteText);

public sealed record AssignCaseRequest(
    Guid AssigneeOfficerUserId);

public sealed record CloseCaseRequest(
    string ResolutionNotes);
