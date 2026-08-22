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
        // Level 5: Home visit with vulnerable senior (BR-SAFETY-02)
        if (locationType == LocationType.SeniorHome && isSubjectVulnerable)
        {
            return new SafetyLevelResult(
                RequiredSafetyLevel: 5,
                RequiredTrustLevel: 5,
                RequiresOrganizationApproval: true,
                RequiresNamedCoordinator: true);
        }

        // Level 4: Inside senior's home or private vehicle transport
        if (locationType == LocationType.SeniorHome || transportMode == TransportMode.VolunteerPrivateVehicle)
        {
            return new SafetyLevelResult(
                RequiredSafetyLevel: 4,
                RequiredTrustLevel: 4,
                RequiresOrganizationApproval: true,
                RequiresNamedCoordinator: false);
        }

        // Level 3: One-to-one in semi-private or institutional settings (doctor, clinic)
        if (locationType is LocationType.Institution or LocationType.Organization)
        {
            return new SafetyLevelResult(
                RequiredSafetyLevel: 3,
                RequiredTrustLevel: 3,
                RequiresOrganizationApproval: false,
                RequiresNamedCoordinator: false);
        }

        // Level 2: One-to-one in public space (accompanied shopping, walks)
        if (locationType == LocationType.PublicPlace)
        {
            return new SafetyLevelResult(
                RequiredSafetyLevel: 2,
                RequiredTrustLevel: 2,
                RequiresOrganizationApproval: false,
                RequiresNamedCoordinator: false);
        }

        // Level 1: Default public/group activity
        return new SafetyLevelResult(
            RequiredSafetyLevel: category.DefaultSafetyLevel > 0 ? category.DefaultSafetyLevel : 1,
            RequiredTrustLevel: category.DefaultSafetyLevel > 0 ? category.DefaultSafetyLevel : 1,
            RequiresOrganizationApproval: false,
            RequiresNamedCoordinator: false);
    }
}
