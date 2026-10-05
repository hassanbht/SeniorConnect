using System.Text.Json;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Wave-3: the interactive API surface (OpenAPI JSON + Scalar + /swagger) is an
/// explicit opt-in, never an environment implication. Staging previously
/// published it by default: any host that was not exactly "Production" mapped
/// the docs. The mapping now requires a positive `ApiDocs:Enabled` flag, off
/// by default in every committed configuration.
/// </summary>
public sealed class ApiDocsOptInTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "backend", "SeniorConnect.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static JsonDocument ReadApiSettings(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Program_maps_docs_only_behind_the_positive_flag()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", "Program.cs"));

        program.Should().Contain("ApiDocs:Enabled",
            "docs must be gated on an explicit opt-in flag");
        program.Should().NotContain("!app.Environment.IsProduction())",
            "no environment may implicitly publish the interactive surface");
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    public void Committed_configurations_default_the_docs_to_off(string fileName)
    {
        using var doc = ReadApiSettings(fileName);

        doc.RootElement.TryGetProperty("ApiDocs", out var apiDocs).Should().BeTrue(
            $"{fileName} must declare the flag explicitly");
        apiDocs.TryGetProperty("Enabled", out var enabled).Should().BeTrue();
        enabled.GetBoolean().Should().BeFalse(
            $"{fileName} must never ship with the docs enabled");
    }
}