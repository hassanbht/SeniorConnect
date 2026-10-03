using System.Text.RegularExpressions;
using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

/// <summary>
/// Local, lightweight, rule-based moderation service for the Community MVP.
/// Runs entirely in-process — no external/cloud LLM call on personal data.
/// Covers spam/scam, harassment, money/bank requests, PII leakage, and
/// obvious threats. Always escalates safeguarding-sensitive categories to a
/// human moderator; it never makes a final decision on those cases.
/// </summary>
public sealed partial class RuleBasedCommunityModerationService : ICommunityModerationService
{
    private const string Version = "rule-based-1.0";

    private static readonly string[] ScamPhrases =
    [
        "dringend geld", "gewinnspiel", "sie haben gewonnen", "klicken sie hier",
        "verifizieren sie ihr konto", "urgent action required", "you have won",
        "click this link", "verify your account", "sofort handeln", "act now",
        "limited time", "nur heute", "exklusives angebot"
    ];

    private static readonly string[] MoneyRequestPhrases =
    [
        "iban", "bic", "kontodaten", "bankverbindung", "vorauszahlung",
        "bitte überweise", "bitte überweisen", "geld schicken", "kreditkartennummer",
        "wire transfer", "bank details", "send money", "gift card", "geschenkkarte",
        "paypal", "bitcoin", "krypto"
    ];

    private static readonly string[] HarassmentPhrases =
    [
        "idiot", "dumm wie", "hure", "schlampe", "halt die klappe",
        "arschloch", "verrecke", "sterb", "du behinderte"
    ];

    private static readonly string[] ThreatPhrases =
    [
        "ich töte", "ich bringe dich um", "du wirst leiden", "ich verprügle",
        "ich will kill", "i will hurt", "i will kill you"
    ];

    private static readonly string[] SafeguardingPhrases =
    [
        "missbrauch", "sexuell", "kinder", "kind", "minderjährig",
        "vergewaltigung", "selbstmord", "suizid", "ich töte mich",
        "ich bringe mich um"
    ];

    [GeneratedRegex(@"(\+?\d[\d\s\-/]{6,}\d)")]
    private static partial Regex PhoneNumberPattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{10,30}\b")]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"\b\d{4}\s?[A-ZÄÖÜa-zäöü][\wÄÖÜäöüß.\-]+\s?\d{1,4}\b")]
    private static partial Regex StreetAddressPattern();

    public Task<ModerationResult> EvaluateAsync(ModerationContext context, CancellationToken ct = default)
    {
        var content = context.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(CreateResult(ModerationDecision.Allow, 1.0, "empty", null));
        }

        var normalized = content.ToLowerInvariant();

        // Safeguarding-sensitive content must always escalate to a human.
        if (ContainsAny(normalized, SafeguardingPhrases))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.EscalateToHuman,
                0.95,
                "safeguarding_sensitive",
                "Inhalt mit möglichem Schutzbedarf erkannt."));
        }

        if (ContainsAny(normalized, ThreatPhrases))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.EscalateToHuman,
                0.92,
                "threat",
                "Mögliche Drohung erkannt."));
        }

        if (ContainsAny(normalized, ScamPhrases))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.HideTemporarily,
                0.88,
                "scam",
                "Möglicher Betrugsversuch erkannt."));
        }

        if (ContainsAny(normalized, MoneyRequestPhrases) || IbanPattern().IsMatch(content))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.HideTemporarily,
                0.90,
                "financial_request",
                "Anfrage nach Geld- oder Bankdaten erkannt."));
        }

        if (ContainsAny(normalized, HarassmentPhrases))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.Flag,
                0.85,
                "harassment",
                "Möglicherweise beleidigender Inhalt erkannt."));
        }

        if (PhoneNumberPattern().IsMatch(content) || StreetAddressPattern().IsMatch(content))
        {
            return Task.FromResult(CreateResult(
                ModerationDecision.Flag,
                0.80,
                "possible_pii",
                "Möglicherweise persönliche Daten (Telefonnummer/Adresse) im Text erkannt."));
        }

        return Task.FromResult(CreateResult(ModerationDecision.Allow, 1.0, "clean", null));
    }

    private static ModerationResult CreateResult(ModerationDecision decision, double confidence, string category, string? reason)
    {
        return new ModerationResult(
            decision,
            confidence,
            category,
            reason,
            Version,
            DateTimeOffset.UtcNow);
    }

    private static bool ContainsAny(string haystackLower, string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystackLower.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
