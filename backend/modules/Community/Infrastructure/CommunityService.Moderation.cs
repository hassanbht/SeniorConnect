using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Infrastructure;

/// <summary>
/// BR-COMM-05 "held for human review": the queue of held posts, held thread
/// messages, held groups/events and open reports, and its resolve action.
/// Platform moderators see everything; group managers see their groups'
/// posts, messages and reports; organization staff additionally see their
/// organization's groups and events. Anything outside the caller's scope is 404.
/// </summary>
public sealed partial class CommunityService
{
    private sealed record ModerationScope(bool IsPlatform, List<Guid> GroupIds, List<Guid> OrgIds)
    {
        public bool CoversGroup(Guid? groupId) => IsPlatform || (groupId is { } g && GroupIds.Contains(g));

        public bool CoversOrg(Guid? orgId) => IsPlatform || (orgId is { } o && OrgIds.Contains(o));
    }

    private async Task<ModerationScope> GetModerationScopeAsync(Guid userId, bool isPlatformModerator, CancellationToken ct)
    {
        if (isPlatformModerator)
        {
            return new ModerationScope(true, [], []);
        }

        var orgIds = (await _orgReader.GetActiveMembershipsForUserAsync(userId, ct))
            .Select(o => o.OrganizationId)
            .ToList();

        var managedGroupIds = await _db.GroupMemberships
            .Where(m => m.UserId == userId && m.Status == GroupMembershipStatus.Active
                && (m.Role == GroupMemberRole.Owner || m.Role == GroupMemberRole.Admin || m.Role == GroupMemberRole.Moderator))
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        var orgGroupIds = orgIds.Count == 0
            ? []
            : await _db.CommunityGroups
                .Where(g => g.OrganizationId != null && orgIds.Contains(g.OrganizationId.Value))
                .Select(g => g.Id)
                .ToListAsync(ct);

        return new ModerationScope(false, managedGroupIds.Concat(orgGroupIds).Distinct().ToList(), orgIds);
    }

    public async Task<Result<IReadOnlyList<ModerationQueueItemDto>>> GetModerationQueueAsync(
        Guid callerUserId,
        bool isPlatformModerator,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetModerationScopeAsync(callerUserId, isPlatformModerator, cancellationToken);
        var isPlatform = scope.IsPlatform;
        var groupIds = scope.GroupIds;
        var orgIds = scope.OrgIds;
        var items = new List<ModerationQueueItemDto>();

        var posts = await _db.CommunityPosts.AsNoTracking()
            .Where(p => p.Status == CommunityPostStatus.PendingReview && (isPlatform || groupIds.Contains(p.GroupId)))
            .OrderBy(p => p.CreatedAtUtc)
            .Take(ModerationQueuePageSize)
            .ToListAsync(cancellationToken);
        items.AddRange(posts.Select(p => new ModerationQueueItemDto(
            ModerationQueueItemType.Post, p.Id, p.GroupId, p.AuthorUserId, $"{p.Title}\n{p.Content}".Trim(), p.ModerationReason, p.CreatedAtUtc)));

        var coveredEventIds = _db.CommunityEvents
            .Where(e => (e.GroupId != null && groupIds.Contains(e.GroupId.Value))
                || (e.OrganizationId != null && orgIds.Contains(e.OrganizationId.Value)))
            .Select(e => e.Id);
        var coveredThreadIds = _db.MessageThreads
            .Where(t => (t.ContextType == ThreadContextType.Group && groupIds.Contains(t.ContextId))
                || (t.ContextType == ThreadContextType.Event && coveredEventIds.Contains(t.ContextId)))
            .Select(t => t.Id);

        var messages = await _db.ThreadMessages.AsNoTracking()
            .Where(m => m.IsFlaggedForModeration && (isPlatform || coveredThreadIds.Contains(m.ThreadId)))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(ModerationQueuePageSize)
            .ToListAsync(cancellationToken);
        items.AddRange(messages.Select(m => new ModerationQueueItemDto(
            ModerationQueueItemType.Message, m.Id, null, m.SenderUserId, m.Content, m.ModerationReason, m.CreatedAtUtc)));

        var groups = await _db.CommunityGroups.AsNoTracking()
            .Where(g => g.Status == GroupStatus.Draft
                && (isPlatform || (g.OrganizationId != null && orgIds.Contains(g.OrganizationId.Value))))
            .OrderBy(g => g.CreatedAtUtc)
            .Take(ModerationQueuePageSize)
            .ToListAsync(cancellationToken);
        items.AddRange(groups.Select(g => new ModerationQueueItemDto(
            ModerationQueueItemType.Group, g.Id, g.Id, g.CreatorUserId, $"{g.Name}\n{g.Description}".Trim(), null, g.CreatedAtUtc)));

        var events = await _db.CommunityEvents.AsNoTracking()
            .Where(e => e.IsPendingReview
                && (isPlatform || (e.OrganizationId != null && orgIds.Contains(e.OrganizationId.Value))))
            .OrderBy(e => e.CreatedAtUtc)
            .Take(ModerationQueuePageSize)
            .ToListAsync(cancellationToken);
        items.AddRange(events.Select(e => new ModerationQueueItemDto(
            ModerationQueueItemType.Event, e.Id, e.GroupId, e.HostUserId, $"{e.Title}\n{e.Description}".Trim(), null, e.CreatedAtUtc)));

        var reports = await _db.CommunityReports.AsNoTracking()
            .Where(r => (r.Status == CommunityReportStatus.Open || r.Status == CommunityReportStatus.UnderReview)
                && (isPlatform || (r.GroupId != null && groupIds.Contains(r.GroupId.Value))))
            .OrderBy(r => r.CreatedAtUtc)
            .Take(ModerationQueuePageSize)
            .ToListAsync(cancellationToken);
        items.AddRange(reports.Select(r => new ModerationQueueItemDto(
            ModerationQueueItemType.Report, r.Id, r.GroupId, r.ReporterUserId, r.Description, r.Category.ToString(), r.CreatedAtUtc,
            r.TargetType, r.TargetId)));

        return Result<IReadOnlyList<ModerationQueueItemDto>>.Success(items.OrderBy(i => i.CreatedAtUtc).ToList());
    }

    public async Task<Result> ResolveModerationItemAsync(
        Guid callerUserId,
        bool isPlatformModerator,
        ModerationQueueItemType itemType,
        Guid itemId,
        ResolveModerationItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetModerationScopeAsync(callerUserId, isPlatformModerator, cancellationToken);
        var approve = request.Resolution == ModerationResolution.Approve;
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var outcome = itemType switch
        {
            ModerationQueueItemType.Post => await ResolvePostAsync(scope, callerUserId, itemId, approve, note, cancellationToken),
            ModerationQueueItemType.Message => await ResolveMessageAsync(scope, callerUserId, itemId, approve, note, cancellationToken),
            ModerationQueueItemType.Group => await ResolveGroupAsync(scope, callerUserId, itemId, approve, note, cancellationToken),
            ModerationQueueItemType.Event => await ResolveEventAsync(scope, callerUserId, itemId, approve, note, cancellationToken),
            ModerationQueueItemType.Report => await ResolveReportAsync(scope, callerUserId, itemId, approve, note, cancellationToken),
            _ => Error.Validation("Unknown moderation item type."),
        };

        if (outcome.IsFailure)
        {
            return outcome;
        }

        var saveError = await SaveOrConflictAsync(cancellationToken);
        return saveError is null ? Result.Success() : saveError;
    }

    private static Error? RequireNotOwnContent(ModerationScope scope, Guid callerUserId, Guid authorUserId) =>
        !scope.IsPlatform && callerUserId == authorUserId
            ? new Error("OWN_CONTENT_REVIEW", "You cannot review your own content — another moderator has to.", ErrorKind.Forbidden)
            : null;

    private async Task<Result> ResolvePostAsync(ModerationScope scope, Guid caller, Guid postId, bool approve, string? note, CancellationToken ct)
    {
        var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.Id == postId && p.Status == CommunityPostStatus.PendingReview, ct);
        if (post is null || !scope.CoversGroup(post.GroupId))
        {
            return Error.NotFound("CommunityPost");
        }

        if (RequireNotOwnContent(scope, caller, post.AuthorUserId) is { } ownError)
        {
            return ownError;
        }

        if (approve)
        {
            post.Restore(caller);
        }
        else
        {
            post.Remove(caller, note);
        }

        await CloseReportsOnAsync(CommunityReportTargetType.Post, postId, approve, caller, note, ct);
        RecordDecision(scope, caller, approve, CommunityModerationTargetType.Post, postId, post.GroupId, note);
        return Result.Success();
    }

    private async Task<Result> ResolveMessageAsync(ModerationScope scope, Guid caller, Guid messageId, bool approve, string? note, CancellationToken ct)
    {
        var message = await _db.ThreadMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.IsFlaggedForModeration, ct);
        var thread = message is null ? null : await _db.MessageThreads.AsNoTracking().FirstOrDefaultAsync(t => t.Id == message.ThreadId, ct);
        if (message is null || thread is null || !await MessageThreadInScopeAsync(scope, thread, ct))
        {
            return Error.NotFound("ThreadMessage");
        }

        if (RequireNotOwnContent(scope, caller, message.SenderUserId) is { } ownError)
        {
            return ownError;
        }

        if (approve)
        {
            message.ClearModerationFlag(caller);
        }
        else
        {
            message.SoftDelete();
        }

        await CloseReportsOnAsync(CommunityReportTargetType.Message, messageId, approve, caller, note, ct);
        RecordDecision(scope, caller, approve, CommunityModerationTargetType.Message, messageId,
            thread.ContextType == ThreadContextType.Group ? thread.ContextId : null, note);
        return Result.Success();
    }

    private async Task<bool> MessageThreadInScopeAsync(ModerationScope scope, MessageThread thread, CancellationToken ct)
    {
        if (scope.IsPlatform)
        {
            return true;
        }

        if (thread.ContextType == ThreadContextType.Group)
        {
            return scope.CoversGroup(thread.ContextId);
        }

        var ev = await _db.CommunityEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == thread.ContextId, ct);
        return ev is not null && (scope.CoversGroup(ev.GroupId) || scope.CoversOrg(ev.OrganizationId));
    }

    private async Task<Result> ResolveGroupAsync(ModerationScope scope, Guid caller, Guid groupId, bool approve, string? note, CancellationToken ct)
    {
        var group = await _db.CommunityGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.Status == GroupStatus.Draft, ct);
        if (group is null || !scope.CoversOrg(group.OrganizationId))
        {
            return Error.NotFound("CommunityGroup");
        }

        if (RequireNotOwnContent(scope, caller, group.CreatorUserId) is { } ownError)
        {
            return ownError;
        }

        if (approve)
        {
            group.Activate(caller);
        }
        else
        {
            group.Block(caller);
        }

        RecordDecision(scope, caller, approve, CommunityModerationTargetType.Group, groupId, groupId, note);
        return Result.Success();
    }

    private async Task<Result> ResolveEventAsync(ModerationScope scope, Guid caller, Guid eventId, bool approve, string? note, CancellationToken ct)
    {
        var ev = await _db.CommunityEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.IsPendingReview, ct);
        if (ev is null || !scope.CoversOrg(ev.OrganizationId))
        {
            return Error.NotFound("CommunityEvent");
        }

        if (RequireNotOwnContent(scope, caller, ev.HostUserId) is { } ownError)
        {
            return ownError;
        }

        if (approve)
        {
            ev.ApproveReview(caller);
        }
        else
        {
            ev.Cancel(note ?? "Not approved by moderation.", caller);
        }

        RecordDecision(scope, caller, approve, CommunityModerationTargetType.Event, eventId, ev.GroupId, note);
        return Result.Success();
    }

    /// <summary>
    /// Approve = the reported content is fine (report dismissed, content kept).
    /// Reject = the report is upheld; a reported post / message is taken down,
    /// other targets are left to the dedicated member/group/event actions.
    /// </summary>
    private async Task<Result> ResolveReportAsync(ModerationScope scope, Guid caller, Guid reportId, bool approve, string? note, CancellationToken ct)
    {
        var report = await _db.CommunityReports.FirstOrDefaultAsync(
            r => r.Id == reportId && (r.Status == CommunityReportStatus.Open || r.Status == CommunityReportStatus.UnderReview), ct);
        if (report is null || !scope.CoversGroup(report.GroupId))
        {
            return Error.NotFound("CommunityReport");
        }

        if (!approve && report.TargetType == CommunityReportTargetType.Post)
        {
            var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.Id == report.TargetId, ct);
            post?.Remove(caller, note);
        }
        else if (!approve && report.TargetType == CommunityReportTargetType.Message)
        {
            var message = await _db.ThreadMessages.FirstOrDefaultAsync(m => m.Id == report.TargetId, ct);
            message?.SoftDelete();
        }
        else if (approve)
        {
            // Content judged fine: release it if the report threshold had held it.
            await ReleaseHeldTargetAsync(report.TargetType, report.TargetId, caller, ct);
        }

        await CloseReportsOnAsync(report.TargetType, report.TargetId, approve, caller, note, ct);
        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: caller,
            action: approve ? CommunityModerationActionType.DismissReport : CommunityModerationActionType.UpholdReport,
            targetType: CommunityModerationTargetType.Report,
            targetId: reportId,
            reason: note ?? (approve ? "Report dismissed" : "Report upheld"),
            groupId: report.GroupId,
            isPlatformAction: scope.IsPlatform));
        return Result.Success();
    }

    private async Task ReleaseHeldTargetAsync(CommunityReportTargetType targetType, Guid targetId, Guid caller, CancellationToken ct)
    {
        if (targetType == CommunityReportTargetType.Post)
        {
            var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.Id == targetId, ct);
            if (post?.Status == CommunityPostStatus.PendingReview)
            {
                post.Restore(caller);
            }
        }
        else if (targetType == CommunityReportTargetType.Message)
        {
            var message = await _db.ThreadMessages.FirstOrDefaultAsync(m => m.Id == targetId, ct);
            message?.ClearModerationFlag(caller);
        }
    }

    private async Task CloseReportsOnAsync(
        CommunityReportTargetType targetType, Guid targetId, bool contentApproved, Guid caller, string? note, CancellationToken ct)
    {
        var openReports = await _db.CommunityReports
            .Where(r => r.TargetType == targetType && r.TargetId == targetId
                && (r.Status == CommunityReportStatus.Open || r.Status == CommunityReportStatus.UnderReview))
            .ToListAsync(ct);

        foreach (var report in openReports)
        {
            if (contentApproved)
            {
                report.Dismiss(caller, note);
            }
            else
            {
                report.Resolve(caller, note);
            }
        }
    }

    private void RecordDecision(
        ModerationScope scope, Guid caller, bool approve, CommunityModerationTargetType targetType, Guid targetId, Guid? groupId, string? note) =>
        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: caller,
            action: approve ? CommunityModerationActionType.ApproveHeldContent : CommunityModerationActionType.RejectHeldContent,
            targetType: targetType,
            targetId: targetId,
            reason: note ?? (approve ? "Approved after review" : "Rejected after review"),
            groupId: groupId,
            isPlatformAction: scope.IsPlatform));
}
