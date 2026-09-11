using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Domain;

namespace SeniorConnect.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth_policy");

        authGroup.MapPost("/request-phone-code", async (
            RequestPhoneOtpRequest request,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var ip = httpContext.GetClientIp();
            var result = await identityService.RequestPhoneOtpAsync(request, ip, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestPhoneCode")
        .Produces<string>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        authGroup.MapPost("/verify-phone", async (
            VerifyPhoneOtpRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.VerifyPhoneOtpAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyPhone")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        authGroup.MapPost("/request-email-link", async (
            RequestEmailMagicLinkRequest request,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var ip = httpContext.GetClientIp();
            var result = await identityService.RequestEmailMagicLinkAsync(request, ip, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestEmailLink")
        .Produces<string>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        authGroup.MapPost("/verify-email", async (
            VerifyEmailMagicLinkRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.VerifyEmailMagicLinkAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyEmail")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        authGroup.MapPost("/staff-login", async (
            StaffLoginRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.StaffLoginAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("StaffLogin")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        authGroup.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.RefreshTokenAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("RefreshToken")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/logout", async (
            RefreshTokenRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.LogoutAsync(request.RefreshToken, ct);
            return result.ToHttpResult();
        })
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent);

        authGroup.MapGet("/devices", async (
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.GetActiveDevicesAsync(userId.Value, null, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetDevices")
        .Produces<IReadOnlyList<DeviceSessionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/devices/{id:guid}:revoke", async (
            Guid id,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.RevokeDeviceSessionAsync(userId.Value, id, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("RevokeDevice")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        authGroup.MapPost("/change-phone:initiate", async (
            InitiatePhoneChangeRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.GetClientIp();
            var result = await identityService.InitiatePhoneChangeAsync(userId.Value, request, ip, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("InitiatePhoneChange")
        .Produces<string>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/change-phone:verify", async (
            VerifyPhoneChangeRequest request,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.VerifyPhoneChangeAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("VerifyPhoneChange")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // --- /me endpoints ---
        var meGroup = app.MapGroup("/api/v1/me")
            .WithTags("User Profile")
            .RequireAuthorization();

        meGroup.MapGet("/", async (
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.GetCurrentUserAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMe")
        .Produces<UserSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapPatch("/", async (
            UpdateProfileRequest request,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.UpdateProfileAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateMe")
        .Produces<UserSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapGet("/trust", async (
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.GetTrustLevelAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyTrust")
        .Produces<TrustLevelDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapGet("/capabilities", async (
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.GetCapabilitiesAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyCapabilities")
        .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapPost("/consents", async (
            RecordConsentRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.GetClientIp();
            var result = await identityService.RecordConsentAsync(userId.Value, request, ip, ct);
            return result.ToHttpResult();
        })
        .WithName("RecordOnboardingConsent")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
