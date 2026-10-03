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

    /// <summary>P2-16 / BR-NOTIFY: first-of-month marker so the silent-volunteer
    /// reminder fires at most once per calendar month, however often the
    /// sweep runs.</summary>
    [DataClass(DataClass.Operational)]
    public DateOnly? LastMonthlyReminderMonth { get; private set; }

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

    public void UpdatePreferences(
        string? bio,
        string? postalCode,
        double? latitude,
        double? longitude,
        int maxDistanceKm,
        short maxActivitiesPerWeek,
        bool hasCar,
        bool isAcceptingRequests)
    {
        Bio = bio;
        PostalCode = postalCode;
        Latitude = latitude;
        Longitude = longitude;
        MaxDistanceKm = maxDistanceKm;
        MaxActivitiesPerWeek = maxActivitiesPerWeek;
        HasCar = hasCar;
        IsAcceptingRequests = isAcceptingRequests;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateStatus(bool isAcceptingRequests)
    {
        IsAcceptingRequests = isAcceptingRequests;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Returns false (no-op) if this month's reminder was already sent.</summary>
    public bool TryMarkMonthlyReminderSent(DateOnly firstOfMonth)
    {
        if (LastMonthlyReminderMonth == firstOfMonth)
        {
            return false;
        }

        LastMonthlyReminderMonth = firstOfMonth;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>Direct set — used only to restore a snapshot on a successful
    /// no-show dispute (P3-19). Behaviour-driven updates go through
    /// <see cref="RecordCompletionOutcome"/>.</summary>
    public void UpdateReliability(decimal? score)
    {
        ReliabilityScore = score;
        if (score.HasValue && ActiveSinceUtc is null)
        {
            ActiveSinceUtc = DateTimeOffset.UtcNow;
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private const decimal ReliabilityLearningRate = 0.2m;

    /// <summary>
    /// P3-20: behaviour-only reliability, nudged toward 1.0 on a completed
    /// assignment or toward 0.0 on an uncontested no-show. An exponential
    /// moving average so one bad outcome after a long good history doesn't
    /// crater the score, but a pattern of no-shows does.
    /// </summary>
    public void RecordCompletionOutcome(bool wasReliable)
    {
        var previous = ReliabilityScore ?? 1.0m;
        var target = wasReliable ? 1.0m : 0.0m;
        ReliabilityScore = previous + (target - previous) * ReliabilityLearningRate;
        ActiveSinceUtc ??= DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// P3-20 / ADR-009: users see a WORD, never a number. Stable keys —
    /// mobile maps each to a localized string, never renders the raw score.
    /// </summary>
    [DataClass(DataClass.Operational)]
    public string ReliabilityLabel => ReliabilityScore switch
    {
        null => "New",
        >= 0.85m => "Reliable",
        >= 0.6m => "Developing",
        _ => "NeedsAttention"
    };
}
