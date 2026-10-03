using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public sealed record VoiceParseResult(
    bool IsEmergency,
    string? EmergencyMessage,
    bool IsBlockedCategory,
    string? BlockedReferralMessage,
    string DetectedCategoryCode,
    DateTimeOffset ProposedScheduledStartUtc,
    int ProposedDurationMinutes,
    string ExtractedNotes,
    double ConfidenceScore);

public sealed record VoiceParseRequest(
    string SpokenTranscript,
    string? PreferredLanguage = "de",
    DateTimeOffset? ReferenceTimeUtc = null);

public interface IVoiceRequestParser
{
    Task<Result<VoiceParseResult>> ParseTranscriptAsync(
        VoiceParseRequest request,
        CancellationToken cancellationToken = default);
}
