using System.Reflection;
using FluentAssertions;
using SeniorConnect.Domain;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// ADR-018 §5 / BR-GDPR-07.
///
/// A mutual-aid platform serving newcomers/migrants must NEVER persist or expose
/// legal/residency status, citizenship, asylum status, ethnicity, religion, or case numbers.
/// Exposure of this data could carry severe legal or immigration consequences.
/// </summary>
public sealed class NoSensitiveMigrationDataTests
{
    private static readonly string[] ForbiddenFieldPatterns =
    [
        "residency", "asylum", "visastatus", "citizenship",
        "immigrationstatus", "ethnicity", "religion", "casenumber"
    ];

    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(Entity).Assembly,
        typeof(Modules.Identity.Domain.User).Assembly,
        typeof(Modules.Profiles.Domain.SupportProfile).Assembly,
        typeof(Modules.Organizations.Domain.Organization).Assembly,
        typeof(Modules.HelpRequests.Domain.HelpRequest).Assembly,
        typeof(Modules.TrustSafety.Domain.SafeguardingCase).Assembly,
        typeof(Modules.Reporting.Domain.Funder).Assembly,
        typeof(Modules.Community.Domain.CommunityGroup).Assembly,
        typeof(Modules.Family.Domain.FamilyRelationship).Assembly,
        typeof(Modules.Notifications.Domain.NotificationMessage).Assembly
    ];

    private static IEnumerable<PropertyInfo> AllEntityProperties()
    {
        return DomainAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Entity).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact]
    public void There_are_entity_types_found_in_domain_assemblies()
    {
        AllEntityProperties().Should().NotBeEmpty(
            "entity properties must be discovered to validate forbidden sensitive field rules.");
    }

    [Fact]
    public void No_entity_persists_a_forbidden_sensitive_field()
    {
        var offenders = AllEntityProperties()
            .Where(p => ForbiddenFieldPatterns.Any(f =>
                p.Name.ToLowerInvariant().Contains(f)))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "this field category can carry immigration or legal consequences "
            + "for a real person if it exists at all, regardless of access control — see ADR-018 §5");
    }

    [Fact]
    public void Deliberately_forbidden_field_name_trips_the_rule()
    {
        // Meta-test ensuring pattern matching catches violating names
        var testNames = new[] { "ResidencyStatus", "AsylumApplicationNumber", "UserCitizenship", "EthnicityType" };
        var matches = testNames.Where(n => ForbiddenFieldPatterns.Any(f => n.ToLowerInvariant().Contains(f))).ToList();
        matches.Should().HaveCount(testNames.Length);
    }
}
