namespace SeniorConnect.Modules.Matching.Domain;

public sealed record MatchingCandidate(
    Guid VolunteerUserId,
    double TotalScore,
    double DistanceKm,
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
    double DistanceKm,
    bool IsEligible,
    string? IneligibilityReason);
