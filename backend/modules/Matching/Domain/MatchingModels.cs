namespace SeniorConnect.Modules.Matching.Domain;

public sealed record MatchingCandidate(
    Guid VolunteerUserId,
    double TotalScore,
    /// <summary>
    /// Hotfix: a band code (SeniorConnect.Domain.DistanceBand), never km;
    /// null when either side has no stored location.
    /// </summary>
    string? DistanceBand,
    bool IsPriorMatch,
    double ReliabilityScore,
    ScoreBreakdown Breakdown,
    bool IsEligible,
    IReadOnlyList<string> IneligibilityReasons);

public sealed record ScoreBreakdown(
    double DistanceScore,
    double ContinuityScore,
    double ReliabilityScore,
    double AvailabilityScore,
    string Explanation);

public sealed record MatchingConfig(
    double DistanceWeight = 0.35,
    double ContinuityWeight = 0.30,
    double ReliabilityWeight = 0.20,
    double AvailabilityWeight = 0.15,
    double ColdStartReliability = 0.70);

public sealed record VolunteerFeedItem(
    Guid HelpRequestId,
    string CategoryNameKey,
    int RequiredSafetyLevel,
    DateTimeOffset ScheduledStartUtc,
    int DurationMinutes,
    string LocationCity,
    string? LocationPostalCode,
    /// <summary>
    /// Band code from the volunteer's own stored location to the request's
    /// ~1 km grid cell; null when the volunteer has no stored location.
    /// </summary>
    string? DistanceBand,
    bool IsEligible,
    string? IneligibilityReason,
    int RowVersion);
