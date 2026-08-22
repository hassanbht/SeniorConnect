using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public interface IActivityService
{
    Task<Result<ActivityDto>> LogActivityAsync(
        Guid volunteerUserId,
        LogActivityRequest request,
        ActivitySource source = ActivitySource.SelfLogged,
        CancellationToken cancellationToken = default);

    Task<Result<ActivityDto>> ConfirmActivityAsync(
        Guid activityId,
        Guid confirmingUserId,
        CancellationToken cancellationToken = default);

    Task<Result<ActivityDto>> DisputeActivityAsync(
        Guid activityId,
        Guid disputingUserId,
        DisputeActivityRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ActivityDto>> GetActivityByIdAsync(
        Guid activityId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ActivityDto>>> GetVolunteerActivitiesAsync(
        Guid volunteerUserId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ActivityDto>>> GetOrganizationActivitiesAsync(
        Guid organizationId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ActivityCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ReferralProviderDto>>> GetReferralProvidersAsync(
        string? referralGroup = null,
        string? regionCode = null,
        CancellationToken cancellationToken = default);
}
