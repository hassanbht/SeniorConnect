using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Organizations.Application;

namespace SeniorConnect.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var orgGroup = app.MapGroup("/api/v1/organizations")
            .WithTags("Organizations")
            .RequireAuthorization();

        orgGroup.MapGet("/", async (
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var result = await orgService.ListOrganizationsAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("ListOrganizations")
        .Produces<IReadOnlyList<OrganizationDto>>(StatusCodes.Status200OK);

        orgGroup.MapPost("/", async (
            CreateOrganizationRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await orgService.CreateOrganizationAsync(request, userId, ct);
            return result.ToHttpResult("/api/v1/organizations/" + result.Value?.Id);
        })
        .WithName("CreateOrganization")
        .Produces<OrganizationDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        orgGroup.MapGet("/{id:guid}", async (
            Guid id,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var result = await orgService.GetOrganizationByIdAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetOrganization")
        .Produces<OrganizationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/{id:guid}/branches", async (
            Guid id,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var result = await orgService.GetBranchesAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetBranches")
        .Produces<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK);

        orgGroup.MapPost("/{id:guid}/branches", async (
            Guid id,
            CreateBranchRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await orgService.CreateBranchAsync(id, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateBranch")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/{id:guid}/members", async (
            Guid id,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var result = await orgService.GetMembershipsAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMemberships")
        .Produces<IReadOnlyList<MembershipDto>>(StatusCodes.Status200OK);

        orgGroup.MapPost("/{id:guid}/members", async (
            Guid id,
            AddMembershipRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await orgService.AddMembershipAsync(id, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("AddMembership")
        .Produces<MembershipDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        orgGroup.MapGet("/{id:guid}/policies", async (
            Guid id,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var result = await orgService.GetPoliciesAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetPolicies")
        .Produces<IReadOnlyList<PolicyDto>>(StatusCodes.Status200OK);

        orgGroup.MapPut("/{id:guid}/policies/{key}", async (
            Guid id,
            string key,
            SetPolicyRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await orgService.SetPolicyAsync(id, key, request.PolicyValueJson, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("SetPolicy")
        .Produces<PolicyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
