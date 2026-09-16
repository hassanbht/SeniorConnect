using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Api.Endpoints;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/community")
            .WithTags("Community")
            .RequireAuthorization();

        // --- Groups ---

        group.MapPost("/groups", async (
            CreateCommunityGroupRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CreateGroupAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/community/groups/" + result.Value?.Id);
        })
        .WithName("CreateCommunityGroup")
        .Produces<CommunityGroupDto>(StatusCodes.Status201Created);

        group.MapGet("/groups", async (
            string? category,
            string? postalCode,
            Guid? organizationId,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetGroupsAsync(category, postalCode, organizationId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityGroups")
        .Produces<IReadOnlyList<CommunityGroupDto>>(StatusCodes.Status200OK);

        group.MapGet("/groups/{id:guid}", async (
            Guid id,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetGroupByIdAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityGroupById")
        .Produces<CommunityGroupDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/groups/{id:guid}", async (
            Guid id,
            UpdateCommunityGroupRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.UpdateGroupAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateCommunityGroup")
        .Produces<CommunityGroupDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/join", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.JoinGroupAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("JoinCommunityGroup")
        .Produces<GroupMembershipDto>(StatusCodes.Status200OK);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:approve", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.ApproveMemberAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ApproveGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:reject", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.RejectMemberAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RejectGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}:leave", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.LeaveGroupAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("LeaveCommunityGroup")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Events ---

        group.MapPost("/events", async (
            CreateCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CreateEventAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/community/events/" + result.Value?.Id);
        })
        .WithName("CreateCommunityEvent")
        .Produces<CommunityEventDto>(StatusCodes.Status201Created);

        group.MapGet("/events", async (
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            string? category,
            string? postalCode,
            Guid? groupId,
            Guid? organizationId,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetEventsAsync(fromUtc, toUtc, category, postalCode, groupId, organizationId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityEvents")
        .Produces<IReadOnlyList<CommunityEventDto>>(StatusCodes.Status200OK);

        group.MapGet("/events/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await communityService.GetEventByIdAsync(id, userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityEventById")
        .Produces<CommunityEventDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/events/{id:guid}", async (
            Guid id,
            UpdateCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.UpdateEventAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateCommunityEvent")
        .Produces<CommunityEventDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/events/{id:guid}:cancel", async (
            Guid id,
            CancelCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CancelEventAsync(id, userId.Value, request.Reason, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelCommunityEvent")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/events/{id:guid}:cancel-occurrence", async (
            Guid id,
            CancelEventOccurrenceRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CancelEventOccurrenceAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelCommunityEventOccurrence")
        .Produces<CommunityEventDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/events/{id:guid}/register", async (
            Guid id,
            RegisterEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.RegisterForEventAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("RegisterForCommunityEvent")
        .Produces<EventRegistrationDto>(StatusCodes.Status200OK);

        group.MapPost("/events/{id:guid}/register:cancel", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CancelRegistrationAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelEventRegistration")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Meine Termine ---

        group.MapGet("/my-schedule", async (
            DateTimeOffset? fromUtc,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.GetMyScheduleAsync(userId.Value, fromUtc, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMySchedule")
        .Produces<IReadOnlyList<MyScheduleItemDto>>(StatusCodes.Status200OK);

        // --- Contextual Threads ---

        group.MapGet("/threads/{contextType}/{contextId:guid}", async (
            ThreadContextType contextType,
            Guid contextId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.GetOrCreateThreadAsync(contextType, contextId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetOrCreateContextThread")
        .Produces<MessageThreadDto>(StatusCodes.Status200OK);

        group.MapGet("/threads/{threadId:guid}/messages", async (
            Guid threadId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.GetMessagesAsync(threadId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetThreadMessages")
        .Produces<IReadOnlyList<ThreadMessageDto>>(StatusCodes.Status200OK);

        group.MapPost("/threads/{threadId:guid}/messages", async (
            Guid threadId,
            PostMessageRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.PostMessageAsync(threadId, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("PostThreadMessage")
        .Produces<ThreadMessageDto>(StatusCodes.Status201Created);

        group.MapDelete("/threads/{threadId:guid}/messages/{messageId:guid}", async (
            Guid threadId,
            Guid messageId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.DeleteMessageAsync(threadId, messageId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteThreadMessage")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/threads/{threadId:guid}/messages/{messageId:guid}:report", async (
            Guid threadId,
            Guid messageId,
            ReportMessageRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.ReportMessageAsync(threadId, messageId, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReportThreadMessage")
        .Produces<ThreadMessageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
