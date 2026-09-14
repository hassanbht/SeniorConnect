using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

public enum LivingSituation
{
    Alone,
    WithSpouse,
    WithFamily,
    CareFacility
}

public enum VulnerabilityReason
{
    MobilityImpaired,
    CognitiveImpaired,
    SociallyIsolated,
    Other
}

public sealed record SupportProfileDto(
    Guid UserId,
    LivingSituation LivingSituation,
    ContactMethod PreferredContactMethod,
    bool HasVulnerabilities,
    VulnerabilityReason? VulnerabilityReason,
    string? EmergencyNotes,
    double? Latitude,
    double? Longitude,
    string? AddressCity,
    string? AddressPostalCode);

public sealed record UpdateSupportProfileRequest(
    LivingSituation LivingSituation,
    ContactMethod PreferredContactMethod,
    bool HasVulnerabilities,
    VulnerabilityReason? VulnerabilityReason,
    string? EmergencyNotes,
    double? Latitude,
    double? Longitude,
    string? AddressCity,
    string? AddressPostalCode);

public sealed record VolunteerProfileDto(
    Guid UserId,
    int MaxTravelDistanceKm,
    int MaxHoursPerWeek,
    bool HasCar,
    bool HasDrivingLicense,
    double ReliabilityScore,
    bool IsCurrentlyAvailable,
    double? Latitude,
    double? Longitude,
    string? AddressCity,
    string? AddressPostalCode,
    IReadOnlyList<VolunteerSkillDto> Skills);

public sealed record VolunteerSkillDto(
    Guid SkillId,
    string SkillName,
    bool IsVerified);

public sealed record UpdateVolunteerProfileRequest(
    int MaxTravelDistanceKm,
    int MaxHoursPerWeek,
    bool HasCar,
    bool HasDrivingLicense,
    bool IsCurrentlyAvailable,
    double? Latitude,
    double? Longitude,
    string? AddressCity,
    string? AddressPostalCode,
    IReadOnlyList<Guid>? SkillIds = null);

public sealed record AvailabilitySlotDto(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record UpdateAvailabilityRequest(
    IReadOnlyList<AvailabilitySlotInput> Slots);

public sealed record AvailabilitySlotInput(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record InterestDto(
    Guid Id,
    string Key,
    string Name,
    string? Category,
    string? Icon,
    int DisplayOrder);

public sealed record UpdateUserInterestsRequest(IReadOnlyList<Guid> InterestIds);

public sealed record LanguageDto(
    string Code,
    string Name,
    string NativeName,
    bool IsRtl);

public sealed record SkillDto(
    Guid Id,
    string Key,
    string Name,
    string? Category,
    bool RequiresVerification,
    int DisplayOrder);

public sealed record HelpCategoryDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    int SafetyLevel,
    bool IsBlocked,
    string? ReferralGroup);
