using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ReferenceEndpoints
{
    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var refGroup = app.MapGroup("/api/v1/reference")
            .WithTags("Reference Data");

        refGroup.MapGet("/interests", async (
            IReferenceDataService refService,
            CancellationToken ct) =>
        {
            var result = await refService.GetInterestsAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetInterests")
        .Produces<IReadOnlyList<InterestDto>>(StatusCodes.Status200OK);

        refGroup.MapGet("/languages", async (
            IReferenceDataService refService,
            CancellationToken ct) =>
        {
            var result = await refService.GetLanguagesAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetLanguages")
        .Produces<IReadOnlyList<LanguageDto>>(StatusCodes.Status200OK);

        refGroup.MapGet("/skills", async (
            IReferenceDataService refService,
            CancellationToken ct) =>
        {
            var result = await refService.GetSkillsAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetSkills")
        .Produces<IReadOnlyList<SkillDto>>(StatusCodes.Status200OK);

        return app;
    }
}
