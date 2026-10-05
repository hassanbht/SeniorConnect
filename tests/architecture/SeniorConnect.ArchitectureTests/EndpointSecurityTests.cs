using System.Reflection;
using FluentAssertions;
using SeniorConnect.Domain;
using NetArchTest.Rules;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

public sealed class EndpointSecurityTests
{
    /// <summary>
    /// Wave-3: an authorization failure must always produce the canonical
    /// ProblemDetails body with a stable `code` — never a bare 401/403.
    ///
    /// What is forbidden: `Results.Unauthorized()` and `Results.Forbid()`
    /// inside `SeniorConnect.Api/Endpoints`. Both produce an empty body, which
    /// breaks the client (it switches on `code`) and the product rule that a
    /// refusal must say what is missing and how to fix it.
    ///
    /// What is allowed: `ResultExtensions.UnauthorizedProblem(...)`,
    /// `ResultExtensions.ForbiddenProblem(...)`,
    /// `ResultExtensions.TenantNotFoundProblem(...)` and any `...ToHttpResult()`
    /// path, which all delegate to the single `MapErrorToProblemDetails`.
    /// Framework `Results.*` for success shapes (`Ok`, `Created`, `NoContent`)
    /// are untouched by this rule.
    /// </summary>
    [Fact]
    public void No_endpoint_emits_a_bodyless_authorization_refusal()
    {
        var endpointDirectory = Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", "Endpoints");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(endpointDirectory, "*.cs"))
        {
            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var trimmed = line.Trim();

                // Comments and member definitions are not responses.
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (trimmed.Contains("Results.Unauthorized()", StringComparison.Ordinal)
                    || trimmed.Contains("Results.Forbid()", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{number}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "use ResultExtensions.UnauthorizedProblem/ForbiddenProblem so every refusal carries a stable code");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "backend", "SeniorConnect.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
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
            .Where(t => !typeof(Entity).IsAssignableFrom(t))
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
