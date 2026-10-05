using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Api.Common;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Api.Endpoints;

public sealed record CoordinatorTriageDto(
    int UnconfirmedActivitiesCount,
    int DisputedActivitiesCount,
    int PendingApplicationsCount,
    IReadOnlyList<ActivityDto> RecentUnconfirmedActivities);

public sealed record BulkEntryItem(
    Guid VolunteerUserId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    LocationType LocationType,
    string? Notes,
    InsuranceContext InsuranceContext,
    TransportMode TransportMode = TransportMode.None);

public sealed record BulkEntryRequest(
    Guid OrganizationId,
    IReadOnlyList<BulkEntryItem> Activities);

/// <summary>A rejected bulk row is reported, never silently dropped.</summary>
public sealed record BulkEntryFailureDto(int Index, Guid VolunteerUserId, string Code, string Detail);

public sealed record BulkEntryResultDto(
    IReadOnlyList<ActivityDto> Logged,
    IReadOnlyList<BulkEntryFailureDto> Failures);

/// <summary>
/// S-2 (Option B): a content-free operational follow-up. No safeguarding
/// facts — ever (BR-SG-05).
/// </summary>
public sealed record OperationalTaskDto(Guid UserId, string DisplayName);

public sealed record VolunteerRosterItemDto(
    Guid UserId,
    string DisplayName,
    string? Phone,
    string? Email,
    string RosterStatus,
    // Confirmed hours in this organization (BR-ROSTER-01: completed activities only).
    int TotalHoursLogged,
    // Date of the last confirmed activity in this organization.
    DateOnly? LastActivityDate,
    // S-3 (Option B, 2026-09-30): content-free operational marker. True means
    // "currently unavailable for operational reasons — contact the organization
    // office". It NEVER carries a restriction kind, reason, case reference or
    // officer identity (BR-SG-05); the S-3 tests pin that boundary.
    bool OperationalHold = false);

public sealed record ReactivateVolunteerRequest(
    string? Notes = null);

public sealed record BroadcastVolunteerMessageRequest(
    Guid OrganizationId,
    string Title,
    string Message,
    string? RosterStatusFilter = null);

/// <summary>
/// P2-36 / ADR-020 §1: staff-only, private nomination aid. Never a public
/// ranking, never shown to users, never an automatically-computed "winner" —
/// the platform surfaces the numbers, a coordinator decides.
/// </summary>
public sealed record RecognitionCandidateDto(
    Guid UserId,
    string DisplayName,
    int TotalConfirmedHours,
    int CompletedActivityCount);

public sealed record RecognitionShortlistDto(
    IReadOnlyList<RecognitionCandidateDto> TopVolunteersByHoursGiven,
    IReadOnlyList<RecognitionCandidateDto> TopSubjectsByActivitiesReceived);

/// <summary>
/// Coordinator workspace. EVERY endpoint is org-scoped and staff-only
/// (Coordinator/Admin of that organization) via IOrganizationAccessGuard:
/// non-member → 404, member without the role → 403 CAPABILITY_MISSING. A
/// funder principal holds no org membership and therefore never reaches any
/// of these PII-bearing lists (BR-FUNDER-02). Rosters contain only the org's
/// own active Volunteer members, never "every user on the platform".
/// </summary>
public static class CoordinatorEndpoints
{
    private const int DefaultShortlistSize = 10;
    private const int MaxShortlistSize = 50;

    public static IEndpointRouteBuilder MapCoordinatorEndpoints(this IEndpointRouteBuilder app)
    {
        var coordGroup = app.MapGroup("/api/v1/coordinator")
            .WithTags("Coordinator")
            .RequireAuthorization();

        // S-2 (Option B, 2026-09-30): content-free operational follow-up tasks.
        // The API deliberately exposes NO reason, NO restriction type and NO
        // safeguarding case reference — only "who" and "since when" (BR-SG-05).
        coordGroup.MapGet("/operational-tasks", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            ISafeguardingOperationalReader operationalReader,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var holds = await operationalReader.GetOpenFollowUpUserIdsAsync(organizationId, ct);
            if (holds.Count == 0) return Results.Ok(Array.Empty<OperationalTaskDto>);

            var users = await db.Users
                .Where(u => holds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => new { u.Id, u.DisplayName })
                .ToListAsync(ct);

            return Results.Ok(users.Select(u => new OperationalTaskDto(u.Id, u.DisplayName))
                .ToList());
        })
        .WithName("GetOperationalTasks")
        .Produces<IReadOnlyList<OperationalTaskDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapGet("/triage", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var unconfirmedQuery = db.Activities
                .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Logged && !a.IsDeleted);

            var unconfirmedCount = await unconfirmedQuery.CountAsync(ct);

            var disputedCount = await db.Activities
                .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Disputed && !a.IsDeleted)
                .CountAsync(ct);

            var pendingAppsCount = await db.VolunteerApplications
                .Where(a => a.OrganizationId == organizationId && a.Status == ApplicationStatus.Open)
                .CountAsync(ct);

            var recentUnconfirmed = await ProjectActivities(unconfirmedQuery
                    .OrderByDescending(a => a.LoggedAtUtc)
                    .Take(10))
                .ToListAsync(ct);

            return Results.Ok(new CoordinatorTriageDto(
                UnconfirmedActivitiesCount: unconfirmedCount,
                DisputedActivitiesCount: disputedCount,
                PendingApplicationsCount: pendingAppsCount,
                RecentUnconfirmedActivities: recentUnconfirmed));
        })
        .WithName("GetCoordinatorTriage")
        .Produces<CoordinatorTriageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapGet("/volunteers", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            ISafeguardingOperationalReader operationalReader,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var holds = await operationalReader.GetOpenFollowUpUserIdsAsync(organizationId, ct);
            return Results.Ok((await BuildRosterAsync(db, directory, organizationId, ct))
                .Select(r => r with { OperationalHold = holds.Contains(r.UserId) })
                .ToList());
        })
        .WithName("GetVolunteerRoster")
        .Produces<IReadOnlyList<VolunteerRosterItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapPost("/volunteers/{volunteerUserId:guid}:reactivate", async (
            Guid volunteerUserId,
            Guid organizationId,
            ReactivateVolunteerRequest request,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var volunteerIds = await directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
            if (!volunteerIds.Contains(volunteerUserId))
            {
                return Result.Failure(Error.NotFound("Volunteer")).ToHttpResult();
            }

            // BR-ROSTER-04/05: an invitation only. The volunteer's own
            // "accepting requests" choice is never overridden by staff.
            var dispatch = await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                RecipientUserId: volunteerUserId,
                Category: NotificationCategory.HelpRequests,
                Priority: NotificationPriority.Normal,
                PreferredChannel: NotificationChannel.InApp,
                Title: "Wir würden uns freuen, wieder von dir zu hören",
                Body: request.Notes ?? "Dein Koordinator lädt dich ein, wieder mitzumachen – ganz ohne Verpflichtung."), ct);

            return dispatch.IsSuccess
                ? Results.Ok(new { Message = "Reactivation invitation sent.", VolunteerUserId = volunteerUserId })
                : Result.Failure(dispatch.Error!).ToHttpResult();
        })
        .WithName("ReactivateVolunteer")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapPost("/volunteers/reminders:send-monthly", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            SeniorConnectDbContext db,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            // P2-16 / BR-NOTIFY: idempotent per volunteer per month — see
            // VolunteerEngagementJobs, shared with the daily background sweep.
            var (silentCount, dispatchedCount) = await SeniorConnect.Infrastructure.BackgroundJobs.VolunteerEngagementJobs
                .SendMonthlySilentVolunteerRemindersAsync(db, notificationService, organizationId, ct);

            return Results.Ok(new { SilentVolunteersCount = silentCount, RemindersDispatched = dispatchedCount });
        })
        .WithName("SendMonthlyVolunteerReminders")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-36 / ADR-020 §1: private, coordinator-only recognition shortlist.
        coordGroup.MapGet("/organizations/{organizationId:guid}/recognition-shortlist", async (
            Guid organizationId,
            int? top,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var take = Math.Clamp(top ?? DefaultShortlistSize, 1, MaxShortlistSize);

            var confirmed = db.Activities
                .Where(a => a.OrganizationId == organizationId && !a.IsDeleted && a.Status == ActivityStatus.Confirmed);

            var byVolunteer = await confirmed
                .GroupBy(a => a.VolunteerUserId)
                .Select(g => new { UserId = g.Key, Minutes = g.Sum(a => a.DurationMinutes), Count = g.Count() })
                .OrderByDescending(g => g.Minutes)
                .Take(take)
                .ToListAsync(ct);

            var bySubject = await confirmed
                .Where(a => a.SubjectUserId != null)
                .GroupBy(a => a.SubjectUserId!.Value)
                .Select(g => new { UserId = g.Key, Minutes = g.Sum(a => a.DurationMinutes), Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(take)
                .ToListAsync(ct);

            var allIds = byVolunteer.Select(x => x.UserId).Concat(bySubject.Select(x => x.UserId)).Distinct().ToList();
            var names = await db.Users
                .Where(u => allIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

            return Results.Ok(new RecognitionShortlistDto(
                TopVolunteersByHoursGiven: byVolunteer.Select(g => new RecognitionCandidateDto(
                    g.UserId, names.GetValueOrDefault(g.UserId, "?"), g.Minutes / 60, g.Count)).ToList(),
                TopSubjectsByActivitiesReceived: bySubject.Select(g => new RecognitionCandidateDto(
                    g.UserId, names.GetValueOrDefault(g.UserId, "?"), g.Minutes / 60, g.Count)).ToList()));
        })
        .WithName("GetRecognitionShortlist")
        .Produces<RecognitionShortlistDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapPost("/activities:bulk-entry", async (
            BulkEntryRequest request,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, request.OrganizationId, ct) is { } denied) return denied;

            var volunteerIds = (await directory.GetActiveVolunteerUserIdsAsync(request.OrganizationId, ct)).ToHashSet();
            var logged = new List<ActivityDto>();
            var failures = new List<BulkEntryFailureDto>();

            for (var index = 0; index < request.Activities.Count; index++)
            {
                var item = request.Activities[index];

                // Staff may only log hours for their own organization's volunteers.
                if (!volunteerIds.Contains(item.VolunteerUserId))
                {
                    failures.Add(new BulkEntryFailureDto(index, item.VolunteerUserId, "VOLUNTEER_NOT_IN_ORGANIZATION",
                        "The volunteer is not an active member of this organization."));
                    continue;
                }

                var logResult = await activityService.LogActivityAsync(
                    item.VolunteerUserId,
                    new LogActivityRequest(
                        OrganizationId: request.OrganizationId,
                        SubjectUserId: item.SubjectUserId,
                        CategoryId: item.CategoryId,
                        OccurredOn: item.OccurredOn,
                        DurationMinutes: item.DurationMinutes,
                        LocationType: item.LocationType,
                        Notes: item.Notes,
                        InsuranceContext: item.InsuranceContext,
                        TransportMode: item.TransportMode),
                    source: ActivitySource.CoordinatorLogged,
                    loggedByUserId: user.GetUserId(),
                    cancellationToken: ct);

                if (logResult.IsSuccess && logResult.Value is not null)
                {
                    logged.Add(logResult.Value);
                }
                else
                {
                    failures.Add(new BulkEntryFailureDto(index, item.VolunteerUserId,
                        logResult.Error?.Code ?? "UNKNOWN", logResult.Error?.Detail ?? "The activity could not be logged."));
                }
            }

            return Results.Ok(new BulkEntryResultDto(logged, failures));
        })
        .WithName("BulkEntryActivities")
        .Produces<BulkEntryResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-24 / P2-26: Attention Queue - Expiring Verifications of this
        // organization's volunteers. Includes already-lapsed credentials
        // (negative DaysRemaining) so they keep raising a task until acted on.
        coordGroup.MapGet("/attention/expiring-verifications", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var volunteerIds = await directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
            var now = DateTimeOffset.UtcNow;
            var in30Days = now.AddDays(30);

            var expiring = await db.Verifications
                .Where(v => volunteerIds.Contains(v.UserId)
                         && v.Status == SeniorConnect.Modules.Identity.Domain.VerificationStatus.Verified
                         && v.ValidUntilUtc != null && v.ValidUntilUtc <= in30Days)
                .Select(v => new
                {
                    v.Id,
                    v.UserId,
                    Type = v.Type.ToString(),
                    v.VerifiedAtUtc,
                    v.ValidUntilUtc,
                })
                .OrderBy(v => v.ValidUntilUtc)
                .ToListAsync(ct);

            return Results.Ok(expiring.Select(v => new
            {
                v.Id,
                v.UserId,
                v.Type,
                v.VerifiedAtUtc,
                v.ValidUntilUtc,
                DaysRemaining = (int)(v.ValidUntilUtc!.Value - now).TotalDays
            }));
        })
        .WithName("GetExpiringVerificationsAttention")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P4-14: Attention Queue - this org's inactive volunteers still holding
        // a key. A held key never auto-expires; a coordinator has to follow up.
        coordGroup.MapGet("/attention/inactive-key-holders", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var volunteerIds = await directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
            var heldKeys = await db.KeyCustodies
                .Where(k => k.Status == KeyCustodyStatus.Held
                         && volunteerIds.Contains(k.VolunteerUserId)
                         && (k.OrganizationId == null || k.OrganizationId == organizationId))
                .ToListAsync(ct);

            if (heldKeys.Count == 0)
            {
                return Results.Ok(Array.Empty<object>());
            }

            var holderIds = heldKeys.Select(k => k.VolunteerUserId).Distinct().ToList();
            var lastCompleted = await LastCompletedActivityAsync(db, organizationId, holderIds, ct);
            var thresholds = await directory.GetRosterThresholdsAsync(organizationId, ct);
            var today = RosterStatusPolicy.TodayInVienna();

            var flagged = heldKeys
                .Select(k =>
                {
                    var lastDate = lastCompleted.GetValueOrDefault(k.VolunteerUserId);
                    return new
                    {
                        k.Id,
                        k.SeniorUserId,
                        k.VolunteerUserId,
                        k.KeyTag,
                        k.HandedOverAtUtc,
                        k.ExpectedReturnAtUtc,
                        RosterStatus = RosterStatusPolicy.Compute(lastDate, today, thresholds),
                        LastActivityDate = lastDate
                    };
                })
                .Where(k => k.RosterStatus is RosterStatusPolicy.Inactive or RosterStatusPolicy.NeverActivated)
                .OrderBy(k => k.HandedOverAtUtc)
                .ToList();

            return Results.Ok(flagged);
        })
        .WithName("GetInactiveKeyHoldersAttention")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-28: Attention Queue - Unconfirmed Hours
        coordGroup.MapGet("/attention/unconfirmed-hours", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var unconfirmed = await ProjectActivities(db.Activities
                    .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Logged && !a.IsDeleted)
                    .OrderByDescending(a => a.LoggedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(unconfirmed);
        })
        .WithName("GetUnconfirmedHoursAttention")
        .Produces<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-16: Attention Queue - this org's volunteers who are accepting
        // requests but have no completed activity within the "Active" window.
        coordGroup.MapGet("/attention/silent-volunteers", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var volunteerIds = await directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
            var thresholds = await directory.GetRosterThresholdsAsync(organizationId, ct);
            var activeSince = RosterStatusPolicy.TodayInVienna().AddDays(-thresholds.ActiveDays);

            var accepting = await db.VolunteerProfiles
                .Where(p => p.IsAcceptingRequests && volunteerIds.Contains(p.UserId))
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(ct);

            var recentlyActive = await db.Activities
                .Where(a => a.OrganizationId == organizationId
                         && a.Status == ActivityStatus.Confirmed
                         && a.OccurredOn >= activeSince
                         && !a.IsDeleted)
                .Select(a => a.VolunteerUserId)
                .Distinct()
                .ToListAsync(ct);

            var silentIds = accepting.Except(recentlyActive).ToList();

            var silentVolunteers = await db.Users
                .Where(u => silentIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Phone,
                    u.Email,
                    u.CreatedAtUtc
                })
                .ToListAsync(ct);

            return Results.Ok(silentVolunteers);
        })
        .WithName("GetSilentVolunteersAttention")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-25: Advanced Volunteer Roster Search & Filtering (own roster only)
        coordGroup.MapGet("/volunteers/search", async (
            Guid organizationId,
            string? query,
            string? rosterStatus,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, organizationId, ct) is { } denied) return denied;

            var roster = await BuildRosterAsync(db, directory, organizationId, ct);
            var trimmed = query?.Trim();

            return Results.Ok(roster
                .Where(r => string.IsNullOrEmpty(trimmed)
                         || r.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                         || (r.Email != null && r.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
                .Where(r => string.IsNullOrWhiteSpace(rosterStatus)
                         || string.Equals(r.RosterStatus, rosterStatus, StringComparison.OrdinalIgnoreCase))
                .ToList());
        })
        .WithName("SearchVolunteerRoster")
        .Produces<IReadOnlyList<VolunteerRosterItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P2-35: in-app message to this organization's volunteers who accept
        // requests, optionally filtered by roster status.
        coordGroup.MapPost("/volunteers/broadcast", async (
            BroadcastVolunteerMessageRequest request,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOrganizationDirectory directory,
            SeniorConnectDbContext db,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            if (await AuthorizeStaffAsync(user, guard, request.OrganizationId, ct) is { } denied) return denied;

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            {
                return Result.Failure(Error.Validation("Title and message are required.")).ToHttpResult();
            }

            var roster = await BuildRosterAsync(db, directory, request.OrganizationId, ct);
            var rosterIds = roster
                .Where(r => string.IsNullOrWhiteSpace(request.RosterStatusFilter)
                         || string.Equals(r.RosterStatus, request.RosterStatusFilter, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.UserId)
                .ToList();

            var recipients = await db.VolunteerProfiles
                .Where(p => p.IsAcceptingRequests && rosterIds.Contains(p.UserId))
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(ct);

            var sentCount = 0;
            foreach (var volId in recipients)
            {
                var result = await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                    RecipientUserId: volId,
                    Category: NotificationCategory.HelpRequests,
                    Priority: NotificationPriority.Normal,
                    PreferredChannel: NotificationChannel.InApp,
                    Title: request.Title,
                    Body: request.Message), ct);

                if (result.IsSuccess) sentCount++;
            }

            return Results.Ok(new
            {
                Message = "Broadcast dispatched successfully.",
                TargetCount = recipients.Count,
                DeliveredCount = sentCount
            });
        })
        .WithName("BroadcastVolunteerMessage")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // null when the caller is org staff; otherwise the 401/403/404 to return.
    private static async Task<IResult?> AuthorizeStaffAsync(
        ClaimsPrincipal user, IOrganizationAccessGuard guard, Guid organizationId, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();

        var auth = await guard.EnsureStaffAsync(userId, organizationId, ct);
        return auth.IsFailure ? auth.ToHttpResult() : null;
    }

    // BR-ROSTER-01: last COMPLETED (Confirmed, non-deleted) activity per volunteer in this org.
    private static async Task<Dictionary<Guid, DateOnly?>> LastCompletedActivityAsync(
        SeniorConnectDbContext db, Guid organizationId, IReadOnlyCollection<Guid> volunteerIds, CancellationToken ct) =>
        await db.Activities
            .Where(a => a.OrganizationId == organizationId
                     && a.Status == ActivityStatus.Confirmed
                     && !a.IsDeleted
                     && volunteerIds.Contains(a.VolunteerUserId))
            .GroupBy(a => a.VolunteerUserId)
            .Select(g => new { VolunteerUserId = g.Key, LastDate = g.Max(a => (DateOnly?)a.OccurredOn) })
            .ToDictionaryAsync(g => g.VolunteerUserId, g => g.LastDate, ct);

    private static async Task<IReadOnlyList<VolunteerRosterItemDto>> BuildRosterAsync(
        SeniorConnectDbContext db, IOrganizationDirectory directory, Guid organizationId, CancellationToken ct)
    {
        var volunteerIds = await directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
        if (volunteerIds.Count == 0) return [];

        var thresholds = await directory.GetRosterThresholdsAsync(organizationId, ct);
        var today = RosterStatusPolicy.TodayInVienna();

        var users = await db.Users
            .Where(u => volunteerIds.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new { u.Id, u.DisplayName, u.Phone, u.Email })
            .ToListAsync(ct);

        var stats = await db.Activities
            .Where(a => a.OrganizationId == organizationId
                     && a.Status == ActivityStatus.Confirmed
                     && !a.IsDeleted
                     && volunteerIds.Contains(a.VolunteerUserId))
            .GroupBy(a => a.VolunteerUserId)
            .Select(g => new
            {
                VolunteerUserId = g.Key,
                TotalMinutes = g.Sum(a => a.DurationMinutes),
                LastDate = g.Max(a => (DateOnly?)a.OccurredOn)
            })
            .ToDictionaryAsync(g => g.VolunteerUserId, ct);

        return users
            .Select(u =>
            {
                stats.TryGetValue(u.Id, out var s);
                return new VolunteerRosterItemDto(
                    UserId: u.Id,
                    DisplayName: u.DisplayName,
                    Phone: u.Phone,
                    Email: u.Email,
                    RosterStatus: RosterStatusPolicy.Compute(s?.LastDate, today, thresholds),
                    TotalHoursLogged: (s?.TotalMinutes ?? 0) / 60,
                    LastActivityDate: s?.LastDate);
            })
            .OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IQueryable<ActivityDto> ProjectActivities(IQueryable<Activity> query) =>
        query.Select(a => new ActivityDto(
            a.Id,
            a.OrganizationId,
            a.BranchId,
            a.VolunteerUserId,
            a.SubjectUserId,
            a.CategoryId,
            a.OccurredOn,
            a.DurationMinutes,
            a.LocationType,
            a.Notes,
            a.InsuranceContext,
            a.TransportMode,
            a.Source,
            a.LoggedByUserId,
            a.LoggedAtUtc,
            a.ConfirmedByUserId,
            a.ConfirmedAtUtc,
            a.Status));
}
