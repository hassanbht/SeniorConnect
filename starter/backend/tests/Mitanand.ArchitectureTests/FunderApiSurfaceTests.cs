using System.Reflection;
using FluentAssertions;
using SeniorConnect.SharedKernel;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// BR-FUNDER-02 and ADR-017.
///
/// A Gemeinde is legally not permitted to see the individuals it funds. This
/// file is the mechanical guarantee of that, walking the whole object graph of
/// every funder-visible type so a nested DTO cannot smuggle a name through.
/// </summary>
public sealed class FunderApiSurfaceTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(Modules.Funder.Contracts.FunderImpactRow).Assembly,
    ];

    private static readonly DataClass[] AllowedForFunder =
    [
        DataClass.PublicProfile,
        DataClass.Operational,
    ];

    private static readonly string[] ForbiddenNameFragments =
    [
        "user", "person", "name", "phone", "email", "address", "note",
        "description", "comment", "birth", "senior", "volunteer", "subject",
    ];

    private static IEnumerable<Type> FunderVisibleTypes() =>
        Assemblies.SelectMany(a => a.GetTypes())
                  .Where(t => typeof(IFunderVisible).IsAssignableFrom(t)
                              && t is { IsInterface: false, IsAbstract: false });

    [Fact]
    public void There_is_at_least_one_funder_visible_type()
    {
        // Guards against the whole suite silently passing because a rename
        // made every other test iterate over an empty set.
        FunderVisibleTypes().Should().NotBeEmpty();
    }

    [Fact]
    public void Every_funder_visible_property_is_classified_and_allowed()
    {
        var offenders = new List<string>();

        foreach (var type in FunderVisibleTypes())
        {
            foreach (var property in WalkProperties(type, depth: 0))
            {
                var attribute = property.Declaring
                    .GetCustomAttribute<DataClassAttribute>();

                if (attribute is null)
                {
                    offenders.Add($"{property.Path} — unclassified");
                    continue;
                }

                if (!AllowedForFunder.Contains(attribute.DataClass))
                {
                    offenders.Add($"{property.Path} — {attribute.DataClass}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "no property reachable from the funder namespace may carry data "
            + "classified above Operational");
    }

    [Fact]
    public void No_funder_visible_property_is_named_like_an_identifier()
    {
        var offenders = new List<string>();

        foreach (var type in FunderVisibleTypes())
        {
            foreach (var property in WalkProperties(type, depth: 0))
            {
                var name = property.Declaring.Name.ToLowerInvariant();

                // OrganizationId and OrganizationName are the deliberate
                // exception: a funder funds organizations by name, and an
                // organization is not a person.
                if (name is "organizationid" or "organizationname")
                {
                    continue;
                }

                var match = ForbiddenNameFragments
                    .FirstOrDefault(f => name.Contains(f, StringComparison.Ordinal));

                if (match is not null)
                {
                    offenders.Add($"{property.Path} — contains '{match}'");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a property name that reads like an identifier is either a leak or "
            + "a rename waiting to become one");
    }

    [Fact]
    public void Suppression_removes_every_measure_not_only_the_headcount()
    {
        // Leaving hours visible while hiding the headcount permits inference.
        var row = Modules.Funder.Contracts.FunderSuppression.Suppress(
            organizationId: Guid.NewGuid(),
            organizationName: "Sozialverein Tirol",
            month: new DateOnly(2026, 9, 1),
            categoryCode: "shopping",
            rawPeople: 7,
            rawVolunteers: 4,
            rawActivities: 31,
            rawHours: 46.5m);

        row.IsSuppressed.Should().BeTrue();
        row.PeopleSupported.Should().BeNull();
        row.Volunteers.Should().BeNull();
        row.ActivityCount.Should().BeNull();
        row.Hours.Should().BeNull();
    }

    [Fact]
    public void A_cohort_at_the_threshold_is_not_suppressed()
    {
        var row = Modules.Funder.Contracts.FunderSuppression.Suppress(
            Guid.NewGuid(), "Sozialverein Tirol", new DateOnly(2026, 9, 1),
            "shopping", rawPeople: 10, rawVolunteers: 10,
            rawActivities: 55, rawHours: 90m);

        row.IsSuppressed.Should().BeFalse();
        row.PeopleSupported.Should().Be(10);
    }

    // --- graph walk -----------------------------------------------------------

    private readonly record struct Reachable(PropertyInfo Declaring, string Path);

    private static IEnumerable<Reachable> WalkProperties(
        Type type, int depth, string prefix = "", HashSet<Type>? seen = null)
    {
        if (depth > 6) yield break;

        seen ??= [];
        if (!seen.Add(type)) yield break;

        foreach (var property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            var path = prefix.Length == 0
                ? $"{type.Name}.{property.Name}"
                : $"{prefix}.{property.Name}";

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType)
                               ?? property.PropertyType;

            var elementType = propertyType.IsGenericType
                ? propertyType.GetGenericArguments().FirstOrDefault()
                : null;

            var isComposite = elementType is { IsPrimitive: false }
                              && elementType != typeof(string)
                              && elementType.Namespace?.StartsWith("SeniorConnect", StringComparison.Ordinal) == true;

            if (isComposite)
            {
                foreach (var nested in WalkProperties(elementType!, depth + 1, path, seen))
                {
                    yield return nested;
                }

                continue;
            }

            if (propertyType.Namespace?.StartsWith("SeniorConnect", StringComparison.Ordinal) == true
                && !propertyType.IsEnum)
            {
                foreach (var nested in WalkProperties(propertyType, depth + 1, path, seen))
                {
                    yield return nested;
                }

                continue;
            }

            yield return new Reachable(property, path);
        }
    }
}
