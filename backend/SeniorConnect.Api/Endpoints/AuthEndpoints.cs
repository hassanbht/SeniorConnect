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

        // ADR-021: Email + Password registration + verification
        authGroup.MapPost("/register", async (
            RegisterEmailPasswordRequest request,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var ip = httpContext.GetClientIp();
            var result = await identityService.RegisterEmailPasswordAsync(request, ip, ct);
            return result.ToHttpResult();
        })
        .WithName("RegisterEmailPassword")
        .Produces<string>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        authGroup.MapGet("/verify-email", async (
            string token,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.VerifyEmailRegistrationAsync(new VerifyEmailRegistrationRequest(token), ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyEmailRegistration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status409Conflict);

        authGroup.MapPost("/login", async (
            EmailPasswordLoginRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.EmailPasswordLoginAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("EmailPasswordLogin")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        authGroup.MapPost("/google", async (
            GoogleLoginRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.LoginWithGoogleAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("GoogleLogin")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/id-austria", async (
            IdAustriaLoginRequest request,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var result = await identityService.LoginWithIdAustriaAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("IdAustriaLogin")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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

        // ADR-021: In-profile phone verification
        var mePhoneGroup = app.MapGroup("/api/v1/me/phone")
            .WithTags("User Profile")
            .RequireAuthorization();

        mePhoneGroup.MapPost("/request-verification", async (
            RequestProfilePhoneVerificationRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var ip = httpContext.GetClientIp();
            var result = await identityService.RequestProfilePhoneVerificationAsync(userId.Value, request, ip, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestProfilePhoneVerification")
        .Produces<string>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        mePhoneGroup.MapPost("/verify", async (
            VerifyProfilePhoneRequest request,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await identityService.VerifyProfilePhoneAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyProfilePhone")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

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

        meGroup.MapPost("/totp/enroll", async (
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.EnrollTotpAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("EnrollTotp")
        .Produces<TotpEnrollmentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        meGroup.MapPost("/totp/confirm", async (
            ConfirmTotpRequest request,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await identityService.ConfirmTotpEnrollmentAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmTotpEnrollment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        meGroup.MapPost("/photo", async (
            HttpRequest httpRequest,
            ClaimsPrincipal user,
            IIdentityService identityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!httpRequest.HasFormContentType) return Results.BadRequest();
            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest();

            await using var stream = file.OpenReadStream();
            var result = await identityService.UploadProfilePhotoAsync(userId.Value, stream, file.ContentType, ct);
            return result.ToHttpResult();
        })
        .WithName("UploadProfilePhoto")
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<UserSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
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
