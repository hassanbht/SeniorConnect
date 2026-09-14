using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

public interface IProfileService
{
    Task<Result<SupportProfileDto>> GetSupportProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<SupportProfileDto>> UpsertSupportProfileAsync(Guid userId, UpdateSupportProfileRequest request, CancellationToken cancellationToken = default);

    Task<Result<VolunteerProfileDto>> GetVolunteerProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<VolunteerProfileDto>> UpsertVolunteerProfileAsync(Guid userId, UpdateVolunteerProfileRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetAvailabilityAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateAvailabilityAsync(Guid userId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<InterestDto>>> GetUserInterestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<InterestDto>>> UpdateUserInterestsAsync(Guid userId, UpdateUserInterestsRequest request, CancellationToken cancellationToken = default);
}
