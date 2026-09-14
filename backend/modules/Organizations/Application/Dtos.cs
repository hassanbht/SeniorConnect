using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Application;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string? LegalName,
    OrganizationType Type,
    OrganizationStatus Status,
    string? SupportEmail,
    string? SupportPhone,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateOrganizationRequest(
    string Name,
    OrganizationType Type,
    string? LegalName = null,
    string? SupportEmail = null,
    string? SupportPhone = null);

public sealed record UpdateOrganizationRequest(
    string Name,
    OrganizationType Type,
    string? LegalName = null,
    string? SupportEmail = null,
    string? SupportPhone = null,
    OrganizationStatus Status = OrganizationStatus.Active);

public sealed record BranchDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Address,
    string? PostalCode,
    string? City,
    double? Latitude,
    double? Longitude,
    bool IsActive);

public sealed record CreateBranchRequest(
    string Name,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record MembershipDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid UserId,
    MembershipRole Role,
    MembershipStatus Status,
    DateTimeOffset? JoinedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record AddMembershipRequest(
    Guid UserId,
    MembershipRole Role,
    Guid? BranchId = null);

public sealed record UpdateMembershipRequest(
    MembershipRole Role,
    MembershipStatus Status);

public sealed record PolicyDto(
    string PolicyKey,
    string PolicyValueJson,
    DateTimeOffset UpdatedAtUtc);

public sealed record SetPolicyRequest(
    string PolicyValueJson);

// Intake Forms DTOs (ADR-021, BR-ORG-FORM)
public sealed record OrganizationIntakeFormDto(
    Guid Id,
    Guid OrganizationId,
    FormType FormType,
    string Title,
    string? Description,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateIntakeFormRequest(
    FormType FormType,
    string Title,
    string? Description = null);

public sealed record UpdateIntakeFormRequest(
    string Title,
    string? Description = null);

public sealed record IntakeFormSectionDto(
    Guid Id,
    Guid FormId,
    string Title,
    string? Description,
    int SortOrder);

public sealed record CreateIntakeFormSectionRequest(
    string Title,
    string? Description = null,
    int SortOrder = 0);

public sealed record UpdateIntakeFormSectionRequest(
    string Title,
    string? Description = null,
    int SortOrder = 0);

public sealed record IntakeFormFieldDto(
    Guid Id,
    Guid SectionId,
    string FieldKey,
    string LabelKey,
    FieldType FieldType,
    bool IsRequired,
    string? OptionsJson,
    int SortOrder);

// Nested detail shape for rendering/editing a form (BR-ORG-FORM gap fix — GetIntakeFormAsync needs sections+fields)
public sealed record OrganizationIntakeFormDetailDto(
    Guid Id,
    Guid OrganizationId,
    FormType FormType,
    string Title,
    string? Description,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<IntakeFormSectionDetailDto> Sections);

public sealed record IntakeFormSectionDetailDto(
    Guid Id,
    Guid FormId,
    string Title,
    string? Description,
    int SortOrder,
    IReadOnlyList<IntakeFormFieldDto> Fields);

public sealed record CreateIntakeFormFieldRequest(
    string FieldKey,
    string LabelKey,
    FieldType FieldType,
    bool IsRequired = false,
    string? OptionsJson = null,
    int SortOrder = 0);

public sealed record UpdateIntakeFormFieldRequest(
    string FieldKey,
    string LabelKey,
    FieldType FieldType,
    bool IsRequired = false,
    string? OptionsJson = null,
    int SortOrder = 0);

public sealed record IntakeFormSubmissionDto(
    Guid Id,
    Guid FormId,
    Guid? OrganizationId,
    Guid UserId,
    SubmissionStatus Status,
    string SubmissionDataJson,
    bool CriminalClearanceDeclared,
    DateTimeOffset? CriminalClearanceDeclaredAtUtc,
    bool GdprConsentAccepted,
    DateTimeOffset? GdprConsentAcceptedAtUtc,
    bool EventInvitationOptIn,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    Guid? DecidedByUserId,
    string? ReviewNotes,
    DateTimeOffset CreatedAtUtc);

public sealed record SubmitIntakeFormRequest(
    string SubmissionDataJson,
    bool CriminalClearanceDeclared,
    bool GdprConsentAccepted,
    bool EventInvitationOptIn);

public sealed record DecideIntakeFormSubmissionRequest(
    bool Approve,
    string? ReviewNotes = null);

public sealed record ActivateFwzTemplateRequest(
    FormType FormType);
