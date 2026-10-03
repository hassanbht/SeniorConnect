using Microsoft.AspNetCore.Http;
using SeniorConnect.Infrastructure;

namespace SeniorConnect.Api.Middleware;

/// <summary>
/// Populates <see cref="DefaultTenantContext"/> for organization-scoped requests
/// so the EF org query filters narrow every query to that organization.
///
/// The organization comes from the route (<c>orgId</c> / <c>organizationId</c>,
/// or <c>id</c> under <c>/api/v1/organizations/{id}</c>) or an
/// <c>organizationId</c> query value. Narrowing can only hide rows, never
/// reveal them, so it needs no membership check here — the services still
/// verify that the caller is staff of that organization.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string OrganizationsPrefix = "/api/v1/organizations/";
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, DefaultTenantContext tenant)
    {
        tenant.OrganizationId = ResolveOrganizationId(context);
        return _next(context);
    }

    internal static Guid? ResolveOrganizationId(HttpContext context)
    {
        var routeValues = context.Request.RouteValues;
        foreach (var name in new[] { "orgId", "organizationId" })
        {
            if (Guid.TryParse(routeValues[name]?.ToString(), out var fromRoute))
            {
                return fromRoute;
            }
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith(OrganizationsPrefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(routeValues["id"]?.ToString(), out var orgFromPath))
        {
            return orgFromPath;
        }

        return Guid.TryParse(context.Request.Query["organizationId"], out var fromQuery)
            ? fromQuery
            : null;
    }
}
