using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class ReferenceDataService : IReferenceDataService
{
    private readonly IProfilesDbContext _db;

    public ReferenceDataService(IProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<InterestDto>>> GetInterestsAsync(CancellationToken cancellationToken = default)
    {
        var interests = await _db.Interests
            .Where(i => i.IsActive)
            .OrderBy(i => i.Code)
            .ToListAsync(cancellationToken);

        var dtos = interests.Select(i => new InterestDto(
            i.Id,
            i.Code,
            i.NameKey,
            null,
            null,
            0)).ToList();

        return Result<IReadOnlyList<InterestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<LanguageDto>>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var languages = await _db.Languages
            .ToListAsync(cancellationToken);

        var dtos = languages.Select(l => new LanguageDto(
            l.IsoCode,
            l.NameKey,
            l.NameKey,
            l.IsoCode == "fa" || l.IsoCode == "ar")).ToList();

        return Result<IReadOnlyList<LanguageDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<SkillDto>>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        var skills = await _db.Skills
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var dtos = skills.Select(s => new SkillDto(
            s.Id,
            s.Code,
            s.NameKey,
            null,
            s.RequiresVerification,
            0)).ToList();

        return Result<IReadOnlyList<SkillDto>>.Success(dtos);
    }
}
