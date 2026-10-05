using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            // Server-side only: an organization-scoped group is created on behalf of
            // that (staff-verified) organization; anything else is an individual
            // creator. The client never gets to choose this (BR-COMM-06 / ADR-025).
            var creatorType = request.OrganizationId is { } orgId && orgId != Guid.Empty
                ? CreatorType.VerifiedOrganization
                : CreatorType.Individual;

            var result = await communityService.CreateGroupAsync(userId.Value, creatorType, request, ct);
            return result.ToHttpResult("/api/v1/community/groups/" + result.Value?.Id);
        })
        .WithName("CreateCommunityGroup")
        .Produces<CommunityGroupDto>(StatusCodes.Status201Created);

        group.MapGet("/groups", async (
            string? category,
            string? postalCode,
            string? city,
            string? region,
            string? language,
            GroupType? type,
            Guid? organizationId,
            string? q,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetGroupsAsync(category, postalCode, city, region, language, type, organizationId, q, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityGroups")
        .Produces<IReadOnlyList<CommunityGroupDto>>(StatusCodes.Status200OK);

        group.MapGet("/groups/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetGroupByIdAsync(id, user.GetUserId(), ct);
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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.UpdateGroupAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateCommunityGroup")
        .Produces<CommunityGroupDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}:archive", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.ArchiveGroupAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ArchiveCommunityGroup")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/join", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.JoinGroupAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("JoinCommunityGroup")
        .Produces<GroupMembershipDto>(StatusCodes.Status200OK);

        group.MapPost("/groups/{id:guid}/invitations/{token}:accept", async (
            Guid id,
            string token,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.AcceptInvitationAsync(id, token, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("AcceptCommunityInvitation")
        .Produces<GroupMembershipDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/groups/{id:guid}/members", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.GetGroupMembersAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityGroupMembers")
        .Produces<IReadOnlyList<GroupMembershipDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/invite", async (
            Guid id,
            InviteMemberRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.InviteMemberAsync(id, userId.Value, request.InvitedUserId, request.Email, request.Phone, ct);
            return result.ToHttpResult();
        })
        .WithName("InviteCommunityGroupMember")
        .Produces<CommunityInvitationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:approve", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.RejectMemberAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RejectGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:remove", async (
            Guid id,
            Guid targetUserId,
            ModerationReasonRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.RemoveMemberAsync(id, targetUserId, userId.Value, request.Reason ?? "No reason provided.", ct);
            return result.ToHttpResult();
        })
        .WithName("RemoveGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:mute", async (
            Guid id,
            Guid targetUserId,
            MuteMemberRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.MuteMemberAsync(id, targetUserId, userId.Value, request.UntilUtc, request.Reason, ct);
            return result.ToHttpResult();
        })
        .WithName("MuteGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:unmute", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.UnmuteMemberAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("UnmuteGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:ban", async (
            Guid id,
            Guid targetUserId,
            ModerationReasonRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.BanMemberAsync(id, targetUserId, userId.Value, request.Reason ?? "No reason provided.", ct);
            return result.ToHttpResult();
        })
        .WithName("BanGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/members/{targetUserId:guid}:unban", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.UnbanMemberAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("UnbanGroupMember")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/moderators/{targetUserId:guid}", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.AppointModeratorAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("AppointCommunityGroupModerator")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/groups/{id:guid}/moderators/{targetUserId:guid}", async (
            Guid id,
            Guid targetUserId,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.RemoveModeratorAsync(id, targetUserId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RemoveCommunityGroupModerator")
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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.LeaveGroupAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("LeaveCommunityGroup")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Posts / Announcements ---

        group.MapGet("/groups/{id:guid}/posts", async (
            Guid id,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetGroupPostsAsync(id, user.GetUserId(), ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityGroupPosts")
        .Produces<IReadOnlyList<CommunityPostDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{id:guid}/posts", async (
            Guid id,
            CreateCommunityPostRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.CreatePostAsync(id, userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/community/groups/" + id + "/posts/" + result.Value?.Id);
        })
        .WithName("CreateCommunityGroupPost")
        .Produces<CommunityPostDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/groups/{id:guid}/posts/{postId:guid}:hide", async (
            Guid id,
            Guid postId,
            ModerationReasonRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.HidePostAsync(id, postId, userId.Value, request.Reason, ct);
            return result.ToHttpResult();
        })
        .WithName("HideCommunityGroupPost")
        .Produces<CommunityPostDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/groups/{id:guid}/posts/{postId:guid}", async (
            Guid id,
            Guid postId,
            // Explicit [FromBody]: a complex-type parameter on a DELETE is not
            // body-inferred; an implicit inference attempt crashes route
            // building at startup (.NET 10 RequestDelegateFactory rule).
            [Microsoft.AspNetCore.Mvc.FromBody] ModerationReasonRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.RemovePostAsync(id, postId, userId.Value, request.Reason, ct);
            return result.ToHttpResult();
        })
        .WithName("RemoveCommunityGroupPost")
        .Produces<CommunityPostDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Reports ---

        group.MapPost("/reports", async (
            CreateCommunityReportRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.CreateReportAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/community/reports/" + result.Value?.Id);
        })
        .WithName("CreateCommunityReport")
        .Produces<CommunityReportDto>(StatusCodes.Status201Created);

        // B-2 (Option A, 2026-09-30): officer-only manual conversion of an
        // escalated unsafe-behavior report into a safeguarding case. Server-side
        // officer check (BR-SG-02); audit trail inside safeguarding (BR-SG-06).
        // Returns the new case id; full case detail is read from safeguarding.
        group.MapPost("/reports/{reportId:guid}:convert-to-case", async (
            Guid reportId,
            ConvertReportToCaseRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();
            var isOfficer = SeniorConnect.Api.Endpoints.SafeguardingEndpoints.IsSafeguardingOfficer(user);

            var result = await communityService.ConvertReportToCaseAsync(
                reportId, userId.Value, isOfficer, request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/safeguarding/cases/{result.Value}", new { caseId = result.Value })
                : result.ToHttpResult();
        })
        .WithName("ConvertReportToSafeguardingCase")
        .Produces<object>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- Events ---

        group.MapPost("/events", async (
            CreateCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var result = await communityService.GetEventsAsync(fromUtc, toUtc, category, postalCode, groupId, organizationId, user.GetUserId(), ct);
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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.ReportMessageAsync(threadId, messageId, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReportThreadMessage")
        .Produces<ThreadMessageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Moderation review queue (BR-COMM-05) ---
        // Scope is decided in the service: platform moderators see everything,
        // group managers / org staff only their own groups' and orgs' content.

        group.MapGet("/moderation/queue", async (
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.GetModerationQueueAsync(userId.Value, IsCommunityModerator(user), ct);
            return result.ToHttpResult();
        })
        .WithName("GetCommunityModerationQueue")
        .Produces<IReadOnlyList<ModerationQueueItemDto>>(StatusCodes.Status200OK);

        group.MapPost("/moderation/queue/{itemType}/{itemId:guid}:resolve", async (
            ModerationQueueItemType itemType,
            Guid itemId,
            ResolveModerationItemRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await communityService.ResolveModerationItemAsync(
                userId.Value, IsCommunityModerator(user), itemType, itemId, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ResolveCommunityModerationItem")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Platform-wide community moderator — from server-side JWT claims only.</summary>
    private static bool IsCommunityModerator(ClaimsPrincipal user) =>
        user.IsPlatformAdmin() || user.HasClaim("capability", "ModerateCommunity");
}
