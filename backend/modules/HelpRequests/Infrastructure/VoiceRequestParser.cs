using System.Text.RegularExpressions;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class VoiceRequestParser : IVoiceRequestParser
{
    private static readonly string[] EmergencyKeywords =
    [
        "notfall", "schmerz", "herz", "sturz", "gefallen", "blut",
        "atemnot", "bewusstlos", "schlaganfall", "144", "112", "emergency"
    ];

    private static readonly string[] BlockedNursingKeywords =
    [
        "spritze", "medikament dosieren", "strümpfe anziehen", "wundversorgung",
        "verband wechseln", "infusion", "pflegebett", "katheter"
    ];

    public Task<Result<VoiceParseResult>> ParseTranscriptAsync(
        VoiceParseRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = request.SpokenTranscript?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult<Result<VoiceParseResult>>(Error.Validation("SpokenTranscript cannot be empty."));
        }

        var lower = text.ToLowerInvariant();
        var now = request.ReferenceTimeUtc ?? DateTimeOffset.UtcNow;

        // 1. Emergency detection (BR-SCOPE-04, BR-SCOPE-05)
        if (EmergencyKeywords.Any(k => lower.Contains(k)))
        {
            var emergencyResult = new VoiceParseResult(
                IsEmergency: true,
                EmergencyMessage: "Der Text enthält Hinweise auf eine akute Notsituation. Bitte rufen Sie unverzüglich die Notrufnummer 144 (Rettung) oder 112 (Euro-Notruf) an.",
                IsBlockedCategory: false,
                BlockedReferralMessage: null,
                DetectedCategoryCode: "emergency",
                ProposedScheduledStartUtc: now,
                ProposedDurationMinutes: 0,
                ExtractedNotes: text,
                ConfidenceScore: 0.99);

            return Task.FromResult(Result<VoiceParseResult>.Success(emergencyResult));
        }

        // 2. Blocked professional nursing detection (BR-SCOPE-02)
        if (BlockedNursingKeywords.Any(k => lower.Contains(k)))
        {
            var blockedResult = new VoiceParseResult(
                IsEmergency: false,
                EmergencyMessage: null,
                IsBlockedCategory: true,
                BlockedReferralMessage: "Für pflegerische oder medizinische Tätigkeiten vermitteln wir an die mobilen Pflegedienste vor Ort (z. B. Rotes Kreuz, Caritas, Hilfswerk, Volkshilfe).",
                DetectedCategoryCode: "blocked_medical",
                ProposedScheduledStartUtc: now.AddHours(2),
                ProposedDurationMinutes: 60,
                ExtractedNotes: text,
                ConfidenceScore: 0.95);

            return Task.FromResult(Result<VoiceParseResult>.Success(blockedResult));
        }

        // 3. Heuristic category detection
        string category = "accompaniment";
        if (lower.Contains("einkauf") || lower.Contains("billa") || lower.Contains("spar") || lower.Contains("lebensmittel"))
        {
            category = "shopping";
        }
        else if (lower.Contains("arzt") || lower.Contains("spital") || lower.Contains("ordination") || lower.Contains("doktor"))
        {
            category = "doctor";
        }
        else if (lower.Contains("amt") || lower.Contains("behörde") || lower.Contains("post") || lower.Contains("formular"))
        {
            category = "authority";
        }
        else if (lower.Contains("deutsch") || lower.Contains("sprache") || lower.Contains("sprechen") || lower.Contains("üben"))
        {
            category = "language_practice";
        }
        else if (lower.Contains("reparatur") || lower.Contains("glühbirne") || lower.Contains("garten") || lower.Contains("haushalt"))
        {
            category = "home_small";
        }

        // 4. Timing extraction
        var scheduledStart = now.AddHours(3);
        if (lower.Contains("morgen"))
        {
            scheduledStart = now.AddDays(1).Date.AddHours(10); // Tomorrow 10:00
        }
        else if (lower.Contains("heute nachmittag") || lower.Contains("nachmittag"))
        {
            scheduledStart = now.Date.AddHours(14); // Today 14:00
        }
        else if (lower.Contains("übermorgen") || lower.Contains("diese woche"))
        {
            scheduledStart = now.AddDays(2).Date.AddHours(10);
        }

        int durationMinutes = 60;
        if (lower.Contains("2 stunden") || lower.Contains("zwei stunden") || lower.Contains("120 min"))
        {
            durationMinutes = 120;
        }
        else if (lower.Contains("halbe stunde") || lower.Contains("30 min"))
        {
            durationMinutes = 30;
        }

        var result = new VoiceParseResult(
            IsEmergency: false,
            EmergencyMessage: null,
            IsBlockedCategory: false,
            BlockedReferralMessage: null,
            DetectedCategoryCode: category,
            ProposedScheduledStartUtc: scheduledStart,
            ProposedDurationMinutes: durationMinutes,
            ExtractedNotes: text,
            ConfidenceScore: 0.88);

        return Task.FromResult(Result<VoiceParseResult>.Success(result));
    }
}
