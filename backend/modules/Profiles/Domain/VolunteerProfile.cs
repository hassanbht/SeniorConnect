using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class VolunteerProfile : Entity, IAuditable
{
    private VolunteerProfile() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Bio { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? PostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Longitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public int MaxDistanceKm { get; private set; } = 10;

    [DataClass(DataClass.Operational)]
    public short MaxActivitiesPerWeek { get; private set; } = 3;

    [DataClass(DataClass.Operational)]
    public bool HasCar { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsAcceptingRequests { get; private set; } = true;

    [DataClass(DataClass.Operational)]
    public decimal? ReliabilityScore { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ActiveSinceUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static VolunteerProfile Create(
        Guid userId,
        string? bio = null,
        string? postalCode = null,
        double? latitude = null,
        double? longitude = null,
        int maxDistanceKm = 10,
        short maxActivitiesPerWeek = 3,
        bool hasCar = false,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new VolunteerProfile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Bio = bio,
            PostalCode = postalCode,
            Latitude = latitude,
            Longitude = longitude,
            MaxDistanceKm = maxDistanceKm,
            MaxActivitiesPerWeek = maxActivitiesPerWeek,
            HasCar = hasCar,
            IsAcceptingRequests = true,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void UpdateStatus(bool isAcceptingRequests)
    {
        IsAcceptingRequests = isAcceptingRequests;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateReliability(decimal? score)
    {
        ReliabilityScore = score;
        if (score.HasValue && ActiveSinceUtc is null)
        {
            ActiveSinceUtc = DateTimeOffset.UtcNow;
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
