using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class ProfileService : IProfileService
{
    private readonly IProfilesDbContext _db;

    public ProfileService(IProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SupportProfileDto>> GetSupportProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.SupportProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("SupportProfile");
        }

        return Result<SupportProfileDto>.Success(MapSupport(profile));
    }

    public async Task<Result<SupportProfileDto>> UpsertSupportProfileAsync(
        Guid userId,
        UpdateSupportProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.SupportProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = SupportProfile.Create(
                userId: userId,
                postalCode: request.AddressPostalCode,
                city: request.AddressCity,
                latitude: request.Latitude,
                longitude: request.Longitude,
                mobilityNote: request.EmergencyNotes,
                livingSituation: request.LivingSituation.ToString(),
                preferredContactMethod: request.PreferredContactMethod);

            _db.SupportProfiles.Add(profile);
        }
        else
        {
            profile.UpdateDetails(
                addressLine: null,
                postalCode: request.AddressPostalCode,
                city: request.AddressCity,
                latitude: request.Latitude,
                longitude: request.Longitude,
                mobilityNote: request.EmergencyNotes,
                livingSituation: request.LivingSituation.ToString(),
                preferredContactMethod: request.PreferredContactMethod);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<SupportProfileDto>.Success(MapSupport(profile));
    }

    public async Task<Result<VolunteerProfileDto>> GetVolunteerProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.VolunteerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("VolunteerProfile");
        }

        var skills = await _db.VolunteerSkills
            .Where(vs => vs.VolunteerProfileId == profile.Id)
            .Join(_db.Skills, vs => vs.SkillId, s => s.Id, (vs, s) => new VolunteerSkillDto(
                s.Id,
                s.NameKey,
                vs.VerifiedAtUtc.HasValue))
            .ToListAsync(cancellationToken);

        return Result<VolunteerProfileDto>.Success(MapVolunteer(profile, skills));
    }

    public async Task<Result<VolunteerProfileDto>> UpsertVolunteerProfileAsync(
        Guid userId,
        UpdateVolunteerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.VolunteerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = VolunteerProfile.Create(
                userId: userId,
                postalCode: request.AddressPostalCode,
                latitude: request.Latitude,
                longitude: request.Longitude,
                maxDistanceKm: request.MaxTravelDistanceKm,
                maxActivitiesPerWeek: (short)request.MaxHoursPerWeek,
                hasCar: request.HasCar);

            _db.VolunteerProfiles.Add(profile);
        }
        else
        {
            profile.UpdatePreferences(
                bio: null,
                postalCode: request.AddressPostalCode,
                latitude: request.Latitude,
                longitude: request.Longitude,
                maxDistanceKm: request.MaxTravelDistanceKm,
                maxActivitiesPerWeek: (short)request.MaxHoursPerWeek,
                hasCar: request.HasCar,
                isAcceptingRequests: request.IsCurrentlyAvailable);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Update skills if provided
        if (request.SkillIds is not null)
        {
            var existingSkills = await _db.VolunteerSkills
                .Where(vs => vs.VolunteerProfileId == profile.Id)
                .ToListAsync(cancellationToken);

            _db.VolunteerSkills.RemoveRange(existingSkills);

            foreach (var skillId in request.SkillIds)
            {
                _db.VolunteerSkills.Add(VolunteerSkill.Create(profile.Id, skillId));
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var skills = await _db.VolunteerSkills
            .Where(vs => vs.VolunteerProfileId == profile.Id)
            .Join(_db.Skills, vs => vs.SkillId, s => s.Id, (vs, s) => new VolunteerSkillDto(
                s.Id,
                s.NameKey,
                vs.VerifiedAtUtc.HasValue))
            .ToListAsync(cancellationToken);

        return Result<VolunteerProfileDto>.Success(MapVolunteer(profile, skills));
    }

    public async Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetAvailabilityAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var slots = await _db.AvailabilitySlots
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var dtos = slots.Select(s => new AvailabilitySlotDto(
            s.Id,
            s.DayOfWeek,
            s.StartTime,
            s.EndTime)).ToList();

        return Result<IReadOnlyList<AvailabilitySlotDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateAvailabilityAsync(
        Guid userId,
        UpdateAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingSlots = await _db.AvailabilitySlots
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.AvailabilitySlots.RemoveRange(existingSlots);

        foreach (var slot in request.Slots)
        {
            _db.AvailabilitySlots.Add(AvailabilitySlot.Create(
                userId: userId,
                dayOfWeek: slot.DayOfWeek,
                startTime: slot.StartTime,
                endTime: slot.EndTime));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAvailabilityAsync(userId, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<InterestDto>>> GetUserInterestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dtos = await _db.UserInterests
            .Where(ui => ui.UserId == userId)
            .Join(_db.Interests, ui => ui.InterestId, i => i.Id, (ui, i) => new InterestDto(
                i.Id,
                i.Code,
                i.NameKey,
                null,
                null,
                0))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<InterestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<InterestDto>>> UpdateUserInterestsAsync(
        Guid userId,
        UpdateUserInterestsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validInterestIds = await _db.Interests
            .Where(i => i.IsActive && request.InterestIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        if (validInterestIds.Count != request.InterestIds.Distinct().Count())
        {
            return Error.Validation("Unknown interest id.");
        }

        var existing = await _db.UserInterests
            .Where(ui => ui.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.UserInterests.RemoveRange(existing);

        foreach (var interestId in request.InterestIds)
        {
            _db.UserInterests.Add(UserInterest.Create(userId, interestId));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetUserInterestsAsync(userId, cancellationToken);
    }

    private static SupportProfileDto MapSupport(SupportProfile p) => new(
        UserId: p.UserId,
        LivingSituation: Enum.TryParse<LivingSituation>(p.LivingSituation, out var sit) ? sit : LivingSituation.Alone,
        PreferredContactMethod: p.PreferredContactMethod,
        HasVulnerabilities: p.VulnerabilityFlag,
        VulnerabilityReason: null,
        EmergencyNotes: p.MobilityNote,
        Latitude: p.Latitude,
        Longitude: p.Longitude,
        AddressCity: p.City,
        AddressPostalCode: p.PostalCode);

    private static VolunteerProfileDto MapVolunteer(VolunteerProfile p, IReadOnlyList<VolunteerSkillDto> skills) => new(
        UserId: p.UserId,
        MaxTravelDistanceKm: p.MaxDistanceKm,
        MaxHoursPerWeek: p.MaxActivitiesPerWeek,
        HasCar: p.HasCar,
        HasDrivingLicense: p.HasCar,
        ReliabilityScore: (double)(p.ReliabilityScore ?? 1.0m),
        IsCurrentlyAvailable: p.IsAcceptingRequests,
        Latitude: p.Latitude,
        Longitude: p.Longitude,
        AddressCity: null,
        AddressPostalCode: p.PostalCode,
        Skills: skills);
}
