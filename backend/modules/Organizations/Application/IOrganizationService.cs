using SeniorConnect.Domain;

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
}
