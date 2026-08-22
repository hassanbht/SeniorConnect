using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Application;

public interface IOnboardingService
{
    Task<Result<VolunteerApplicationDto>> ApplyAsync(
        Guid userId,
        ApplyVolunteerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VolunteerApplicationDto>> GetApplicationByIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<VolunteerApplicationDto>>> GetOrganizationApplicationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<Result<VolunteerApplicationDto>> UpdateStepAsync(
        Guid applicationId,
        Guid stepId,
        UpdateStepRequest request,
        Guid staffUserId,
        CancellationToken cancellationToken = default);

    Task<Result<VolunteerApplicationDto>> DecideApplicationAsync(
        Guid applicationId,
        DecideApplicationRequest request,
        Guid staffUserId,
        CancellationToken cancellationToken = default);
}
