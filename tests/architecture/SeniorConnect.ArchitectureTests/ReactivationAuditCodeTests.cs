using System.Reflection;
using FluentAssertions;
using SeniorConnect.Api.Endpoints;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// W2-6: coordinator-initiated volunteer reactivation is recorded under a
/// stable machine-readable audit code. A translated sentence (or the
/// coordinator's free-text notes) must never become persistent audit data:
/// audit rows have to stay greppable and comparable across locales.
/// </summary>
public sealed class ReactivationAuditCodeTests
{
    [Fact]
    public void The_reactivation_audit_code_is_stable()
    {
        CoordinatorEndpoints.ReactivatedByCoordinatorAction.Should().Be(
            "REACTIVATED_BY_COORDINATOR",
            "clients, reports and queries match on this code; renaming it silently breaks them");
    }

    [Fact]
    public void The_reactivate_handler_persists_the_code_not_user_prose()
    {
        var path = Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", "Endpoints", "CoordinatorEndpoints.cs");
        var lines = File.ReadAllLines(path);

        lines.Should().Contain(l => l.Contains("ReactivatedByCoordinatorAction", StringComparison.Ordinal),
            "the reactivate handler must write the stable code to the audit trail");

        var auditWrites = lines.Where(l => l.Contains("AuditEntry.Create", StringComparison.Ordinal)).ToList();
        auditWrites.Should().NotBeEmpty();
        auditWrites.Should().OnlyContain(l => !l.Contains("Notes", StringComparison.Ordinal),
            "free-text notes belong in the user-visible notification, never in the audit record");
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
}
