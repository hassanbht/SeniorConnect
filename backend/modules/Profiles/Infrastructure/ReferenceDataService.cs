using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class ReferenceDataService : IReferenceDataService
{
    private readonly IProfilesDbContext _db;
    private readonly IMemoryCache _cache;

    private static readonly MemoryCacheEntryOptions DefaultCacheOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(60))
        .SetSlidingExpiration(TimeSpan.FromMinutes(15));

    public ReferenceDataService(IProfilesDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<InterestDto>>> GetInterestsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "ref_interests";

        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<InterestDto>? dtos) || dtos is null)
        {
            var interests = await _db.Interests
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderBy(i => i.Code)
                .ToListAsync(cancellationToken);

            dtos = interests.Select(i => new InterestDto(
                i.Id,
                i.Code,
                i.NameKey,
                null,
                null,
                0)).ToList();

            _cache.Set(cacheKey, dtos, DefaultCacheOptions);
        }

        return Result<IReadOnlyList<InterestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<LanguageDto>>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "ref_languages";

        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<LanguageDto>? dtos) || dtos is null)
        {
            var languages = await _db.Languages
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            dtos = languages.Select(l => new LanguageDto(
                l.IsoCode,
                l.NameKey,
                l.NameKey,
                l.IsoCode == "fa" || l.IsoCode == "ar")).ToList();

            _cache.Set(cacheKey, dtos, DefaultCacheOptions);
        }

        return Result<IReadOnlyList<LanguageDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<SkillDto>>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "ref_skills";

        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<SkillDto>? dtos) || dtos is null)
        {
            var skills = await _db.Skills
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Code)
                .ToListAsync(cancellationToken);

            dtos = skills.Select(s => new SkillDto(
                s.Id,
                s.Code,
                s.NameKey,
                null,
                s.RequiresVerification,
                0)).ToList();

            _cache.Set(cacheKey, dtos, DefaultCacheOptions);
        }

        return Result<IReadOnlyList<SkillDto>>.Success(dtos);
    }
}