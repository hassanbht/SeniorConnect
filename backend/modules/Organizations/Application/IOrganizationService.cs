using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Application;

public interface IOrganizationService
{
    Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<OrganizationDto>> GetOrganizationByIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrganizationDto>>> ListOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<Result<BranchDto>> CreateBranchAsync(Guid organizationId, CreateBranchRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Result<MembershipDto>> AddMembershipAsync(Guid organizationId, AddMembershipRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<MembershipDto>> UpdateMembershipAsync(Guid organizationId, Guid membershipId, UpdateMembershipRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MembershipDto>>> GetMembershipsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Result<PolicyDto>> SetPolicyAsync(Guid organizationId, string key, string valueJson, Guid? updatedBy, CancellationToken cancellationToken = default);
    Task<Result<PolicyDto>> GetPolicyAsync(Guid organizationId, string key, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PolicyDto>>> GetPoliciesAsync(Guid organizationId, CancellationToken cancellationToken = default);

    // Intake Forms (ADR-021, BR-ORG-FORM)
    Task<Result<OrganizationIntakeFormDto>> CreateIntakeFormAsync(Guid organizationId, CreateIntakeFormRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<OrganizationIntakeFormDetailDto>> GetIntakeFormAsync(Guid organizationId, FormType formType, CancellationToken cancellationToken = default);
    Task<Result<OrganizationIntakeFormDto>> UpdateIntakeFormAsync(Guid organizationId, Guid formId, UpdateIntakeFormRequest request, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormSectionDto>> AddSectionAsync(Guid formId, CreateIntakeFormSectionRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormSectionDto>> UpdateSectionAsync(Guid formId, Guid sectionId, UpdateIntakeFormSectionRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteSectionAsync(Guid formId, Guid sectionId, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormFieldDto>> AddFieldAsync(Guid sectionId, CreateIntakeFormFieldRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormFieldDto>> UpdateFieldAsync(Guid sectionId, Guid fieldId, UpdateIntakeFormFieldRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteFieldAsync(Guid sectionId, Guid fieldId, CancellationToken cancellationToken = default);

    Task<Result<IntakeFormSubmissionDto>> SubmitIntakeFormAsync(Guid formId, Guid organizationId, SubmitIntakeFormRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<IntakeFormSubmissionDto>>> GetSubmissionsAsync(Guid formId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Result<IntakeFormSubmissionDto>> DecideSubmissionAsync(Guid formId, Guid organizationId, Guid submissionId, DecideIntakeFormSubmissionRequest request, Guid decidedByUserId, CancellationToken cancellationToken = default);

    Task<Result> ActivateFwzTemplateAsync(Guid organizationId, ActivateFwzTemplateRequest request, Guid? createdBy, CancellationToken cancellationToken = default);
}
