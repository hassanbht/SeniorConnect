using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

public interface IReferenceDataService
{
    Task<Result<IReadOnlyList<InterestDto>>> GetInterestsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LanguageDto>>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SkillDto>>> GetSkillsAsync(CancellationToken cancellationToken = default);
}
