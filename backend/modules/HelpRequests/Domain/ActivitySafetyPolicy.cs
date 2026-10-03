namespace SeniorConnect.Modules.HelpRequests.Domain;

public interface IActivitySafetyPolicy
{
    SafetyLevelResult Evaluate(
        ActivityCategory category,
        LocationType locationType,
        TransportMode transportMode,
        bool isSubjectVulnerable);
}

public sealed record SafetyLevelResult(
    int RequiredSafetyLevel,
    int RequiredTrustLevel,
    bool RequiresOrganizationApproval,
    bool RequiresNamedCoordinator);

public sealed class ActivitySafetyPolicy : IActivitySafetyPolicy
{
    public SafetyLevelResult Evaluate(
        ActivityCategory category,
        LocationType locationType,
        TransportMode transportMode,
        bool isSubjectVulnerable)
    {
        // BR-SAFETY-02/04: the effective level is the stricter of the
        // category's own default and what the context (location, transport,
        // vulnerability) demands — context can raise a level, never lower it.
        var contextLevel = ContextLevel(locationType, transportMode, isSubjectVulnerable);
        var level = Math.Clamp(Math.Max(contextLevel, category.DefaultSafetyLevel), 1, 5);

        // BR-SAFETY-02 minimum trust: L1→1, L2→2, L3→3, L4→5 + org approval,
        // L5→5 + org approval + named coordinator.
        return level switch
        {
            5 => new SafetyLevelResult(5, 5, RequiresOrganizationApproval: true, RequiresNamedCoordinator: true),
            4 => new SafetyLevelResult(4, 5, RequiresOrganizationApproval: true, RequiresNamedCoordinator: false),
            _ => new SafetyLevelResult(level, level, RequiresOrganizationApproval: false, RequiresNamedCoordinator: false),
        };
    }

    private static int ContextLevel(LocationType locationType, TransportMode transportMode, bool isSubjectVulnerable)
    {
        if (locationType == LocationType.SeniorHome && isSubjectVulnerable)
        {
            return 5; // home visit with a highly vulnerable person
        }

        if (locationType == LocationType.SeniorHome || transportMode == TransportMode.VolunteerPrivateVehicle)
        {
            return 4; // inside the person's home, or alone in a private vehicle
        }

        return locationType switch
        {
            LocationType.Institution or LocationType.Organization => 3, // one-to-one, semi-private / institutional
            LocationType.PublicPlace => 2,                              // one-to-one, public space
            _ => 1,
        };
    }
}
