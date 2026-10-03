using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Application;

/// <summary>
/// Every org-scoped mutation and every staff read takes the caller's user id
/// and authorizes explicitly (non-member → 404, member without role → 403).
/// <c>callerIsPlatformAdmin</c> is computed server-side from token claims by
/// the endpoint, never read from a request body.
/// </summary>
public interface IOrganizationService
{
    /// <summary>Platform admin only — the bootstrap path for a new organization.</summary>
    Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationRequest request, Guid callerUserId, bool callerIsPlatformAdmin, CancellationToken cancellationToken = default);
    Task<Result<OrganizationDto>> GetOrganizationByIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrganizationDto>>> ListOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<Result<BranchDto>> CreateBranchAsync(Guid organizationId, CreateBranchRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Org Admin only; a platform admin may add members (bootstrap of the first Admin).</summary>
    Task<Result<MembershipDto>> AddMembershipAsync(Guid organizationId, AddMembershipRequest request, Guid callerUserId, bool callerIsPlatformAdmin, CancellationToken cancellationToken = default);
    Task<Result<MembershipDto>> UpdateMembershipAsync(Guid organizationId, Guid membershipId, UpdateMembershipRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MembershipDto>>> GetMembershipsAsync(Guid organizationId, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<Result<PolicyDto>> SetPolicyAsync(Guid organizationId, string key, string valueJson, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<PolicyDto>> GetPolicyAsync(Guid organizationId, string key, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PolicyDto>>> GetPoliciesAsync(Guid organizationId, Guid callerUserId, CancellationToken cancellationToken = default);

    // Intake Forms (ADR-021, BR-ORG-FORM) — building and reviewing is staff-only.
    Task<Result<OrganizationIntakeFormDto>> CreateIntakeFormAsync(Guid organizationId, CreateIntakeFormRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<OrganizationIntakeFormDetailDto>> GetIntakeFormAsync(Guid organizationId, FormType formType, CancellationToken cancellationToken = default);
    Task<Result<OrganizationIntakeFormDto>> UpdateIntakeFormAsync(Guid organizationId, Guid formId, UpdateIntakeFormRequest request, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormSectionDto>> AddSectionAsync(Guid organizationId, Guid formId, CreateIntakeFormSectionRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormSectionDto>> UpdateSectionAsync(Guid organizationId, Guid formId, Guid sectionId, UpdateIntakeFormSectionRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result> DeleteSectionAsync(Guid organizationId, Guid formId, Guid sectionId, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormFieldDto>> AddFieldAsync(Guid organizationId, Guid formId, Guid sectionId, CreateIntakeFormFieldRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormFieldDto>> UpdateFieldAsync(Guid organizationId, Guid formId, Guid sectionId, Guid fieldId, UpdateIntakeFormFieldRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result> DeleteFieldAsync(Guid organizationId, Guid formId, Guid sectionId, Guid fieldId, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormSubmissionDto>> SubmitIntakeFormAsync(Guid formId, Guid organizationId, SubmitIntakeFormRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<IntakeFormSubmissionDto>>> GetSubmissionsAsync(Guid formId, Guid organizationId, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormSubmissionDto>> DecideSubmissionAsync(Guid formId, Guid organizationId, Guid submissionId, DecideIntakeFormSubmissionRequest request, Guid decidedByUserId, CancellationToken cancellationToken = default);

    Task<Result> ActivateStandardTemplateAsync(Guid organizationId, ActivateStandardTemplateRequest request, Guid callerUserId, CancellationToken cancellationToken = default);
}
