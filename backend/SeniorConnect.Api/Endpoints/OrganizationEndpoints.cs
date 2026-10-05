using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Api.Endpoints;

/// <summary>
/// Every org-scoped mutation and staff read passes the authenticated caller to
/// IOrganizationService, which authorizes explicitly (non-member → 404,
/// member without the role → 403 CAPABILITY_MISSING).
/// </summary>
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
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.CreateOrganizationAsync(request, userId, user.IsPlatformAdmin(), ct);
            return result.ToHttpResult("/api/v1/organizations/" + result.Value?.Id);
        })
        .WithName("CreateOrganization")
        .Produces<OrganizationDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

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
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.CreateBranchAsync(id, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateBranch")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/{id:guid}/members", async (
            Guid id,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.GetMembershipsAsync(id, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMemberships")
        .Produces<IReadOnlyList<MembershipDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapPost("/{id:guid}/members", async (
            Guid id,
            AddMembershipRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.AddMembershipAsync(id, request, userId, user.IsPlatformAdmin(), ct);
            return result.ToHttpResult();
        })
        .WithName("AddMembership")
        .Produces<MembershipDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        orgGroup.MapGet("/{id:guid}/policies", async (
            Guid id,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.GetPoliciesAsync(id, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetPolicies")
        .Produces<IReadOnlyList<PolicyDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapPut("/{id:guid}/policies/{key}", async (
            Guid id,
            string key,
            SetPolicyRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.SetPolicyAsync(id, key, request.PolicyValueJson, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("SetPolicy")
        .Produces<PolicyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Intake Forms (ADR-021, BR-ORG-FORM)
        var formsGroup = orgGroup.MapGroup("/{id:guid}/forms")
            .WithTags("Organization Intake Forms");

        formsGroup.MapPost("/", async (
            Guid id,
            CreateIntakeFormRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.CreateIntakeFormAsync(id, request, userId, ct);
            return result.ToHttpResult("/api/v1/organizations/" + id + "/forms/" + result.Value?.Id);
        })
        .WithName("CreateIntakeForm")
        .Produces<OrganizationIntakeFormDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapGet("/{formType}", async (
            Guid id,
            string formType,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<FormType>(formType, true, out var parsedFormType))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid formType. Must be 'volunteer' or 'help_seeker'.",
                    extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });
            }
            var result = await orgService.GetIntakeFormAsync(id, parsedFormType, ct);
            return result.ToHttpResult();
        })
        .WithName("GetIntakeForm")
        .Produces<OrganizationIntakeFormDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPut("/{formId:guid}", async (
            Guid id,
            Guid formId,
            UpdateIntakeFormRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.UpdateIntakeFormAsync(id, formId, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateIntakeForm")
        .Produces<OrganizationIntakeFormDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPost("/{formId:guid}/sections", async (
            Guid id,
            Guid formId,
            CreateIntakeFormSectionRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.AddSectionAsync(id, formId, request, userId, ct);
            return result.ToHttpResult("/api/v1/organizations/" + id + "/forms/" + formId + "/sections/" + result.Value?.Id);
        })
        .WithName("AddIntakeFormSection")
        .Produces<IntakeFormSectionDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPut("/{formId:guid}/sections/{sectionId:guid}", async (
            Guid id,
            Guid formId,
            Guid sectionId,
            UpdateIntakeFormSectionRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.UpdateSectionAsync(id, formId, sectionId, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateIntakeFormSection")
        .Produces<IntakeFormSectionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapDelete("/{formId:guid}/sections/{sectionId:guid}", async (
            Guid id,
            Guid formId,
            Guid sectionId,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.DeleteSectionAsync(id, formId, sectionId, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteIntakeFormSection")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPost("/{formId:guid}/sections/{sectionId:guid}/fields", async (
            Guid id,
            Guid formId,
            Guid sectionId,
            CreateIntakeFormFieldRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.AddFieldAsync(id, formId, sectionId, request, userId, ct);
            return result.ToHttpResult("/api/v1/organizations/" + id + "/forms/" + formId + "/sections/" + sectionId + "/fields/" + result.Value?.Id);
        })
        .WithName("AddIntakeFormField")
        .Produces<IntakeFormFieldDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPut("/{formId:guid}/sections/{sectionId:guid}/fields/{fieldId:guid}", async (
            Guid id,
            Guid formId,
            Guid sectionId,
            Guid fieldId,
            UpdateIntakeFormFieldRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.UpdateFieldAsync(id, formId, sectionId, fieldId, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateIntakeFormField")
        .Produces<IntakeFormFieldDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapDelete("/{formId:guid}/sections/{sectionId:guid}/fields/{fieldId:guid}", async (
            Guid id,
            Guid formId,
            Guid sectionId,
            Guid fieldId,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.DeleteFieldAsync(id, formId, sectionId, fieldId, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteIntakeFormField")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPost("/{formId:guid}/submissions", async (
            Guid id,
            Guid formId,
            SubmitIntakeFormRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.SubmitIntakeFormAsync(formId, id, request, userId, ct);
            return result.ToHttpResult("/api/v1/organizations/" + id + "/forms/" + formId + "/submissions/" + result.Value?.Id);
        })
        .WithName("SubmitIntakeForm")
        .Produces<IntakeFormSubmissionDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapGet("/{formId:guid}/submissions", async (
            Guid id,
            Guid formId,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.GetSubmissionsAsync(formId, id, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetIntakeFormSubmissions")
        .Produces<IReadOnlyList<IntakeFormSubmissionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        formsGroup.MapPut("/{formId:guid}/submissions/{submissionId:guid}:decide", async (
            Guid id,
            Guid formId,
            Guid submissionId,
            DecideIntakeFormSubmissionRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.DecideSubmissionAsync(formId, id, submissionId, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("DecideIntakeFormSubmission")
        .Produces<IntakeFormSubmissionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        formsGroup.MapPost("/{formId:guid}:activate-template", async (
            Guid id,
            Guid formId,
            ActivateStandardTemplateRequest request,
            ClaimsPrincipal user,
            IOrganizationService orgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await orgService.ActivateStandardTemplateAsync(id, request, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("ActivateStandardTemplate")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
