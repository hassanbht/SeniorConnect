using System.Text.Json;
using FluentAssertions;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.HelpRequests.Application;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Phase 2 (docs/architecture/reference-data-cache.md): the reference DTOs the
/// Flutter app caches keep a stable JSON shape and carry nothing personal.
/// Adding a field here means reviewing it against the cache's security
/// boundary first.
/// </summary>
public sealed class ReferenceDataContractTests
{
    // Same naming policy the API uses (ASP.NET Core web defaults: camelCase).
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string[] JsonFields(object dto) =>
        JsonDocument.Parse(JsonSerializer.Serialize(dto, dto.GetType(), Web))
            .RootElement.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public void Gemeinde_contract_is_code_name_postalCode_and_the_municipality_centroid()
    {
        JsonFields(new GemeindeDto("70101", "Innsbruck", "6020", 47.2692, 11.4041))
            .Should().Equal("code", "name", "postalCode", "latitude", "longitude");
    }

    [Fact]
    public void Bundesland_and_Bezirk_contracts_are_code_and_name()
    {
        JsonFields(new BundeslandDto("7", "Tirol")).Should().Equal("code", "name");
        JsonFields(new BezirkDto("701", "Innsbruck-Stadt")).Should().Equal("code", "name");
    }

    [Fact]
    public void Activity_category_contract_is_global_reference_data()
    {
        JsonFields(new ActivityCategoryDto(Guid.Empty, "shopping", "help.category.shopping", 1, false, null, true))
            .Should().Equal("id", "code", "nameKey", "defaultSafetyLevel", "isBlocked", "referralGroup", "isActive");
    }

    [Theory]
    [InlineData(typeof(BundeslandDto))]
    [InlineData(typeof(BezirkDto))]
    [InlineData(typeof(GemeindeDto))]
    [InlineData(typeof(ActivityCategoryDto))]
    public void Reference_dtos_carry_no_personal_tenant_or_session_fields(Type dto)
    {
        string[] forbidden =
        [
            "userid", "displayname", "addressline", "phone", "phonenumber", "email",
            "organizationid", "tenantid", "token", "accesstoken", "refreshtoken",
            "helprequestid", "safeguardingcaseid",
        ];

        dto.GetProperties().Select(p => p.Name.ToLowerInvariant())
            .Should().NotIntersectWith(forbidden);
    }
}
