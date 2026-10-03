using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Notifications.Application;

namespace SeniorConnect.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications & Communication")
            .RequireAuthorization();

        group.MapGet("/", async (
            bool? unreadOnly,
            ClaimsPrincipal user,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await notificationService.GetUserNotificationsAsync(userId.Value, unreadOnly ?? false, ct);
            return result.ToHttpResult();
        })
        .WithName("GetUserNotifications")
        .Produces<IReadOnlyList<NotificationMessageDto>>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}:read", async (
            Guid id,
            ClaimsPrincipal user,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await notificationService.MarkAsReadAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("MarkNotificationAsRead")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/preferences", async (
            ClaimsPrincipal user,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await notificationService.GetPreferencesAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetNotificationPreferences")
        .Produces<NotificationPreferenceDto>(StatusCodes.Status200OK);

        group.MapPut("/preferences", async (
            UpdateNotificationPreferenceRequest request,
            ClaimsPrincipal user,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await notificationService.UpdatePreferencesAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateNotificationPreferences")
        .Produces<NotificationPreferenceDto>(StatusCodes.Status200OK);

        group.MapPost("/dispatch", async (
            DispatchNotificationRequest request,
            ClaimsPrincipal user,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await notificationService.DispatchNotificationAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("DispatchNotification")
        .Produces<NotificationMessageDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
