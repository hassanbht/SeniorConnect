using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

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
    string? Motivation);

public sealed record UpdateStepRequest(
    StepStatus Status,
    string? Note);

public sealed record DecideApplicationRequest(
    bool Approved,
    string? Reason);
