namespace SeniorConnect.Modules.HelpRequests.Domain;

/// <summary>
/// BR-SCOPE-02/03 (P3-04): nursing/medical tasks are never ordinary volunteer
/// help, whether chosen as a category, typed as notes or spoken. Single keyword
/// list shared by typed request creation and the voice parser.
/// </summary>
public static class NursingTaskDetector
{
    /// <summary>Seeded code of the blocked category whose referral group is returned.</summary>
    public const string NursingCategoryCode = "nursing_blocked";

    private static readonly string[] Keywords =
    [
        "spritze", "insulin", "medikament dosieren", "strümpfe anziehen", "wundversorgung",
        "verband wechseln", "infusion", "pflegebett", "katheter"
    ];

    public static bool IsNursingTask(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
