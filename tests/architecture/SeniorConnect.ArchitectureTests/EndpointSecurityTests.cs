using System.Reflection;
using FluentAssertions;
using SeniorConnect.Domain;
using NetArchTest.Rules;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

public sealed class EndpointSecurityTests
{
    /// <summary>
    /// Nothing arrives from a client that the server should decide.
    /// docs/architecture/authorization.md §6.
    /// </summary>
    private static readonly string[] ServerOwnedProperties =
    [
        "TrustLevel", "SafetyLevel", "RequiredTrustLevel", "RequiredSafetyLevel",
        "Capabilities", "Capability", "Role", "Roles", "IsVerified",
        "MatchScore", "ReliabilityScore", "RosterStatus",
    ];

    private static IEnumerable<Type> RequestDtos() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.StartsWith("SeniorConnect", StringComparison.Ordinal) == true)
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal)
                     || t.Name.EndsWith("Command", StringComparison.Ordinal));

    [Fact]
    public void No_inbound_request_carries_a_server_owned_value()
    {
        var offenders = new List<string>();

        foreach (var dto in RequestDtos())
        {
            foreach (var property in dto.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance))
            {
                if (ServerOwnedProperties.Contains(property.Name, StringComparer.Ordinal))
                {
                    // Organization membership role assignment by coordinators is allowed, but system-level roles are not
                    if (property.PropertyType.Name == "MembershipRole")
                    {
                        continue;
                    }

                    offenders.Add($"{dto.Name}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a client-supplied trust level, safety level or capability is a "
            + "privilege-escalation bug, not a convenience");
    }

    [Fact]
    public void Every_persisted_property_is_data_classified()
    {
        // A new field cannot be added without someone deciding what it is.
        // docs/architecture/privacy-gdpr.md §1.
        var entities = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(Entity)) && !t.IsAbstract);

        var offenders = new List<string>();

        foreach (var entity in entities)
        {
            foreach (var property in entity.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == nameof(Entity.Id)
                    || property.Name == nameof(Entity.DomainEvents))
                {
                    continue;
                }

                if (property.GetCustomAttribute<DataClassAttribute>() is null
                    && property.CanWrite is false && property.GetMethod?.IsVirtual is true)
                {
                    continue; // computed / derived
                }

                if (property.GetCustomAttribute<DataClassAttribute>() is null)
                {
                    offenders.Add($"{entity.Name}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "every persisted property needs a [DataClass] so access policy and "
            + "the funder surface test can reason about it");
    }
}
