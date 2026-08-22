using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.SharedKernel;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// BR-TENANT-03 and ADR-007.
///
/// These are not style tests. A missing query filter is a cross-organization
/// data leak, and "the developer will remember" is not a control. Do not delete
/// or skip anything in this file.
/// </summary>
public sealed class TenantIsolationTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time")
            .Options;

        using var context = new SeniorConnectDbContext(options, new StubTenant());
        return context.Model;
    }

    [Fact]
    public void Every_organization_scoped_entity_has_a_query_filter()
    {
        var model = BuildModel();

        var offenders = model.GetEntityTypes()
            .Where(e => typeof(IOrganizationScoped).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "every IOrganizationScoped entity must have an EF global query "
            + "filter, or one organization can read another's rows");
    }

    [Fact]
    public void Every_soft_deletable_entity_has_a_query_filter()
    {
        var model = BuildModel();

        var offenders = model.GetEntityTypes()
            .Where(e => typeof(ISoftDeletable).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// ADR-007. The single most likely way the org-optional thesis dies is
    /// someone "tidying up" a nullable column. This test is that decision's
    /// only real defence.
    /// </summary>
    [Fact]
    public void OrganizationId_is_nullable_on_every_scoped_entity()
    {
        var model = BuildModel();

        var offenders = new List<string>();

        foreach (var entity in model.GetEntityTypes()
                     .Where(e => typeof(IOrganizationScoped).IsAssignableFrom(e.ClrType)))
        {
            var property = entity.FindProperty(nameof(IOrganizationScoped.OrganizationId));

            if (property is null || !property.IsNullable)
            {
                offenders.Add(entity.ClrType.Name);
            }
        }

        offenders.Should().BeEmpty(
            "a null OrganizationId means 'independent / community', which is a "
            + "first-class case (ADR-007), not a missing value");
    }

    /// <summary>
    /// The filter must not exclude community rows. A filter of the shape
    /// `x.OrganizationId == tenant.OrganizationId` compiles, passes the
    /// "has a filter" test above, and silently hides every community activity.
    /// </summary>
    [Fact]
    public void Query_filters_admit_rows_with_a_null_organization()
    {
        var model = BuildModel();

        var offenders = model.GetEntityTypes()
            .Where(e => typeof(IOrganizationScoped).IsAssignableFrom(e.ClrType))
            .Where(e =>
            {
                var expression = e.GetQueryFilter()?.ToString() ?? string.Empty;
                return !expression.Contains("OrganizationId == null",
                    StringComparison.Ordinal);
            })
            .Select(e => e.ClrType.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "the query filter must explicitly admit OrganizationId == null, "
            + "otherwise community rows become invisible");
    }

    private sealed class StubTenant : ITenantContext
    {
        public Guid? OrganizationId => Guid.Empty;
        public bool IsPlatformScope => false;
    }
}
