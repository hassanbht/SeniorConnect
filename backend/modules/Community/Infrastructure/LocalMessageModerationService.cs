using System.Text.RegularExpressions;
using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

/// <summary>
/// Local, lightweight, rule-based screen for BR-COMM-05 (ADR-020). Runs
/// entirely in-process — no external/cloud call, no personal data leaves the
/// process. Covers the four categories the pilot partner asked for: scam
/// patterns, requests for money/bank details, harassment, and personal data
/// pasted into a public context. Held messages go to a coordinator review
/// queue (surfaced by ThreadMessage.IsFlaggedForModeration), never silently
/// deleted and never silently shown.
/// </summary>
public sealed partial class LocalMessageModerationService : IMessageModerationService
{
    private static readonly string[] MoneyRequestPhrases =
    [
        "iban", "bic", "kontodaten", "bankverbindung", "vorauszahlung",
        "bitte überweise", "bitte überweisen", "geld schicken", "kreditkartennummer",
        "wire transfer", "bank details", "send money", "gift card", "geschenkkarte",
    ];

    private static readonly string[] ScamPhrases =
    [
        "dringend geld", "gewinnspiel", "sie haben gewonnen", "klicken sie hier",
        "verifizieren sie ihr konto", "urgent action required", "you have won",
        "click this link", "verify your account",
    ];

    private static readonly string[] HarassmentPhrases =
    [
        "idiot", "dumm wie", "hure", "schlampe", "halt die klappe",
    ];

    [GeneratedRegex(@"(\+?\d[\d\s\-/]{6,}\d)")]
    private static partial Regex PhoneNumberPattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{10,30}\b")]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"\b\d{4}\s?[A-ZÄÖÜa-zäöü][\wÄÖÜäöüß.\-]+\s?\d{1,4}\b")]
    private static partial Regex StreetAddressPattern();

    public ModerationVerdict Screen(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ModerationVerdict.Clean;
        }

        var normalized = content.ToLowerInvariant();

        if (ContainsAny(normalized, ScamPhrases))
        {
            return ModerationVerdict.Flag("Möglicher Betrugsversuch erkannt.");
        }

        if (ContainsAny(normalized, MoneyRequestPhrases) || IbanPattern().IsMatch(content))
        {
            return ModerationVerdict.Flag("Anfrage nach Geld- oder Bankdaten erkannt.");
        }

        if (ContainsAny(normalized, HarassmentPhrases))
        {
            return ModerationVerdict.Flag("Möglicherweise beleidigender Inhalt erkannt.");
        }

        if (PhoneNumberPattern().IsMatch(content) || StreetAddressPattern().IsMatch(content))
        {
            return ModerationVerdict.Flag("Möglicherweise persönliche Daten (Telefonnummer/Adresse) im Text erkannt.");
        }

        return ModerationVerdict.Clean;
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
