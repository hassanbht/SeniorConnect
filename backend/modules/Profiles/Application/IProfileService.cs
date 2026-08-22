using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

public interface IProfileService
{
    Task<Result<SeniorProfileDto>> GetSeniorProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<SeniorProfileDto>> UpsertSeniorProfileAsync(Guid userId, UpdateSeniorProfileRequest request, CancellationToken cancellationToken = default);

    Task<Result<VolunteerProfileDto>> GetVolunteerProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<VolunteerProfileDto>> UpsertVolunteerProfileAsync(Guid userId, UpdateVolunteerProfileRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetAvailabilityAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateAvailabilityAsync(Guid userId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default);
}
