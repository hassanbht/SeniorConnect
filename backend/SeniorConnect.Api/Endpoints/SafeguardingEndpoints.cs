using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Api.Common;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Api.Endpoints;

public static class SafeguardingEndpoints
{
    public static IEndpointRouteBuilder MapSafeguardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/safeguarding")
            .WithTags("Safeguarding")
            .RequireAuthorization();

        // Anyone can report a concern (2-tap workflow from mobile, BR-SG-04)
        group.MapPost("/concerns", async (
            RaiseConcernRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await safeguardingService.RaiseConcernAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/safeguarding/cases/" + result.Value?.Id);
        })
        .WithName("RaiseSafeguardingConcern")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // Safeguarding Officer Only Endpoints (BR-SG-02)
        group.MapGet("/cases", async (
            Guid? organizationId,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to access safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.GetCasesAsync(organizationId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSafeguardingCases")
        .Produces<IReadOnlyList<SafeguardingCaseDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/cases/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to access safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.GetCaseByIdAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSafeguardingCaseById")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}/notes", async (
            Guid id,
            AddCaseNoteRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to update safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.AddCaseNoteAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("AddSafeguardingCaseNote")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}:assign", async (
            Guid id,
            AssignCaseRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to assign safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.AssignCaseAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("AssignSafeguardingCase")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}:close", async (
            Guid id,
            CloseCaseRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to close safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.CloseCaseAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CloseSafeguardingCase")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Phase 4 (docs/security/safeguarding-location-policy.md): restrictions
        // on the subject of a case the officer may see. Officer-only; the
        // tenant comes from the case row.
        group.MapGet("/cases/{id:guid}/restrictions", async (
            Guid id, ClaimsPrincipal user, ISafeguardingService safeguardingService, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (!IsSafeguardingOfficer(user)) return OfficerRequired();
            return (await safeguardingService.GetRestrictionsAsync(id, userId.Value, ct)).ToHttpResult();
        })
        .WithName("GetSafeguardingRestrictions")
        .Produces<IReadOnlyList<SafeguardingRestrictionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}/restrictions", async (
            Guid id, ImposeRestrictionRequest request, ClaimsPrincipal user,
            ISafeguardingService safeguardingService, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (!IsSafeguardingOfficer(user)) return OfficerRequired();
            return (await safeguardingService.ImposeRestrictionAsync(id, userId.Value, request, ct)).ToHttpResult();
        })
        .WithName("ImposeSafeguardingRestriction")
        .Produces<SafeguardingRestrictionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // ---- S-4: platform safeguarding officer administration (2026-09-30) ----
        // Organization-less cases are owned exclusively by users holding the
        // PlatformSafeguardingOfficer role (an org-less membership). Only a
        // PlatformAdmin may appoint or remove them. Every change is audited.

        group.MapPost("/platform-officers/{officerUserId:guid}", async (
            Guid officerUserId, ClaimsPrincipal user,
            SeniorConnectDbContext db, IOrganizationsDbContext orgs, CancellationToken ct) =>
        {
            var callerId = user.GetUserId();
            if (callerId is null) return Results.Unauthorized();
            if (!IsPlatformAdmin(user)) return PlatformAdminRequired();

            var user2 = await db.Users.FirstOrDefaultAsync(u => u.Id == officerUserId && !u.IsDeleted, ct);
            if (user2 is null) return Results.NotFound(new { code = "NOT_FOUND" });

            var exists = await orgs.OrganizationMemberships.AnyAsync(m =>
                m.UserId == officerUserId && m.OrganizationId == null &&
                m.Role == MembershipRole.PlatformSafeguardingOfficer &&
                (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Invited), ct);
            if (exists)
            {
                return Results.Conflict(new { code = "PLATFORM_OFFICER_EXISTS", detail = "User is already a platform safeguarding officer." });
            }

            var membership = OrganizationMembership.Create(null, officerUserId, MembershipRole.PlatformSafeguardingOfficer);
            membership.Activate();
            orgs.OrganizationMemberships.Add(membership);
            db.AuditEntries.Add(AuditEntry.Create(
                action: "CREATE_PLATFORM_SAFEGUARDING_OFFICER",
                subjectType: "OrganizationMembership",
                subjectId: membership.Id,
                actorUserId: callerId));
            await db.SaveChangesAsync(ct);
            await orgs.SaveChangesAsync(ct);
            return Results.Ok(new { membershipId = membership.Id, officerUserId });
        })
        .WithName("AppointPlatformSafeguardingOfficer")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/platform-officers/{officerUserId:guid}", async (
            Guid officerUserId, ClaimsPrincipal user,
            SeniorConnectDbContext db, IOrganizationsDbContext orgs, CancellationToken ct) =>
        {
            var callerId = user.GetUserId();
            if (callerId is null) return Results.Unauthorized();
            if (!IsPlatformAdmin(user)) return PlatformAdminRequired();

            var memberships = await orgs.OrganizationMemberships
                .Where(m => m.UserId == officerUserId && m.OrganizationId == null &&
                            m.Role == MembershipRole.PlatformSafeguardingOfficer &&
                            m.Status == MembershipStatus.Active)
                .ToListAsync(ct);
            if (memberships.Count == 0)
            {
                return Results.NotFound(new { code = "NOT_FOUND" });
            }

            foreach (var m in memberships)
            {
                m.Suspend(); // removal = suspension of the platform-officer membership (BR-SG-03: never deleted)
            }
            db.AuditEntries.Add(AuditEntry.Create(
                action: "REMOVE_PLATFORM_SAFEGUARDING_OFFICER",
                subjectType: "OrganizationMembership",
                subjectId: memberships[0].Id,
                actorUserId: callerId));
            await db.SaveChangesAsync(ct);
            await orgs.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("RemovePlatformSafeguardingOfficer")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/restrictions/{restrictionId:guid}:lift", async (
            Guid restrictionId, LiftRestrictionRequest request, ClaimsPrincipal user,
            ISafeguardingService safeguardingService, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (!IsSafeguardingOfficer(user)) return OfficerRequired();
            return (await safeguardingService.LiftRestrictionAsync(restrictionId, userId.Value, request, ct)).ToHttpResult();
        })
        .WithName("LiftSafeguardingRestriction")
        .Produces<SafeguardingRestrictionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static IResult PlatformAdminRequired() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden",
        detail: "The capability 'PlatformAdmin' is required to manage platform safeguarding officers.",
        extensions: new Dictionary<string, object?> { ["code"] = "PLATFORM_ADMIN_REQUIRED" });

    private static IResult OfficerRequired() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden",
        detail: "The capability 'SafeguardingOfficer' is required.",
        extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });

    /// <summary>
    /// BR-SG-02: only the explicit SafeguardingOfficer capability grants access.
    /// PlatformAdmin and OrganizationAdmin do not imply it.
    /// </summary>
    /// <summary>
    /// S-4 (2026-09-30): PLATFORM-level officer check, server-side. Only a
    /// PlatformAdmin principal may appoint or remove platform officers.
    /// </summary>
    internal static bool IsPlatformAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole("PlatformAdmin")
            || user.HasClaim("capability", "ManageCompliance")
            || user.HasClaim("capability", "PlatformAdmin");
    }

    internal static bool IsSafeguardingOfficer(ClaimsPrincipal user)
    {
        return user.IsInRole("SafeguardingOfficer")
            || user.IsInRole("PlatformSafeguardingOfficer")
            || user.HasClaim("capability", "SafeguardingOfficer")
            || user.HasClaim("capability", "PlatformSafeguardingOfficer")
            || user.HasClaim("capability", "ManageSafeguarding");
    }
}
