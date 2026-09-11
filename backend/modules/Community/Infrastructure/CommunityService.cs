using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Infrastructure;

public sealed class CommunityService : ICommunityService
{
    private readonly ICommunityDbContext _db;
    private readonly IMessageModerationService _moderation;

    public CommunityService(ICommunityDbContext db, IMessageModerationService moderation)
    {
        _db = db;
        _moderation = moderation;
    }

    // --- Groups ---

    public async Task<Result<CommunityGroupDto>> CreateGroupAsync(
        Guid userId,
        CreateCommunityGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var groupResult = CommunityGroup.Create(
            creatorUserId: userId,
            title: request.Title,
            description: request.Description,
            category: request.Category,
            scope: request.Scope,
            joinPolicy: request.JoinPolicy,
            organizationId: request.OrganizationId,
            locationPostalCode: request.LocationPostalCode,
            maxMembers: request.MaxMembers);

        if (groupResult.IsFailure)
        {
            return groupResult.Error!;
        }

        var group = groupResult.Value!;
        _db.CommunityGroups.Add(group);

        // Creator is automatic Owner
        var ownerMembership = GroupMembership.CreateOwner(group.Id, userId);
        _db.GroupMemberships.Add(ownerMembership);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount: 1));
    }

    public async Task<Result<CommunityGroupDto>> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var memberCount = await _db.GroupMemberships
            .CountAsync(m => m.GroupId == groupId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount));
    }

    public async Task<Result<IReadOnlyList<CommunityGroupDto>>> GetGroupsAsync(
        string? category = null,
        string? postalCode = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.CommunityGroups
            .Where(g => !g.IsDeleted && !g.IsArchived);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(g => g.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            var post = postalCode.Trim();
            query = query.Where(g => g.LocationPostalCode == post);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(g => g.OrganizationId == organizationId);
        }

        var groups = await query
            .OrderByDescending(g => g.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();

        var memberCounts = await _db.GroupMemberships
            .Where(m => groupIds.Contains(m.GroupId) && m.Status == GroupMembershipStatus.Active)
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.GroupId, v => v.Count, cancellationToken);

        var dtos = groups.Select(g => MapGroup(g, memberCounts.GetValueOrDefault(g.Id, 0))).ToList();
        return Result<IReadOnlyList<CommunityGroupDto>>.Success(dtos);
    }

    public async Task<Result<CommunityGroupDto>> UpdateGroupAsync(
        Guid groupId,
        Guid userId,
        UpdateCommunityGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (membership is null || (membership.Role != GroupMemberRole.Owner && membership.Role != GroupMemberRole.Admin))
        {
            return Error.Forbidden("Only group owners or admins can update the group.");
        }

        var updateResult = group.Update(
            title: request.Title,
            description: request.Description,
            category: request.Category,
            scope: request.Scope,
            joinPolicy: request.JoinPolicy,
            locationPostalCode: request.LocationPostalCode,
            maxMembers: request.MaxMembers,
            updatedByUserId: userId);

        if (updateResult.IsFailure)
        {
            return updateResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var memberCount = await _db.GroupMemberships
            .CountAsync(m => m.GroupId == groupId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount));
    }

    public async Task<Result<GroupMembershipDto>> JoinGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var existing = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == GroupMembershipStatus.Active)
            {
                return Error.Conflict("You are already an active member of this group.");
            }
            if (existing.Status == GroupMembershipStatus.PendingApproval)
            {
                return Error.Conflict("Your membership request is already pending approval.");
            }
        }

        GroupMembership membership;
        if (group.JoinPolicy == GroupJoinPolicy.Open)
        {
            membership = GroupMembership.JoinDirectly(groupId, userId);
        }
        else if (group.JoinPolicy == GroupJoinPolicy.RequestApproval)
        {
            membership = GroupMembership.RequestToJoin(groupId, userId);
        }
        else
        {
            return Error.Validation("This group is invite-only.");
        }

        _db.GroupMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<GroupMembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result> ApproveMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var adminMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == adminUserId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (adminMembership is null || (adminMembership.Role != GroupMemberRole.Owner && adminMembership.Role != GroupMemberRole.Admin))
        {
            return Error.Forbidden("Only group owners or admins can approve member requests.");
        }

        var targetMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (targetMembership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var approveResult = targetMembership.Approve(adminUserId);
        if (approveResult.IsFailure)
        {
            return approveResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var adminMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == adminUserId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (adminMembership is null || (adminMembership.Role != GroupMemberRole.Owner && adminMembership.Role != GroupMemberRole.Admin))
        {
            return Error.Forbidden("Only group owners or admins can reject member requests.");
        }

        var targetMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (targetMembership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var rejectResult = targetMembership.Reject(adminUserId);
        if (rejectResult.IsFailure)
        {
            return rejectResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> LeaveGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        membership.Leave();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // --- Events ---

    public async Task<Result<CommunityEventDto>> CreateEventAsync(
        Guid userId,
        CreateCommunityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var eventResult = CommunityEvent.Create(
            hostUserId: userId,
            title: request.Title,
            description: request.Description,
            startsAtUtc: request.StartsAtUtc,
            endsAtUtc: request.EndsAtUtc,
            category: request.Category,
            groupId: request.GroupId,
            organizationId: request.OrganizationId,
            locationAddress: request.LocationAddress,
            locationPostalCode: request.LocationPostalCode,
            recurrenceFrequency: request.RecurrenceFrequency,
            recurrenceUntilUtc: request.RecurrenceUntilUtc,
            capacity: request.Capacity);

        if (eventResult.IsFailure)
        {
            return eventResult.Error!;
        }

        var ev = eventResult.Value!;
        _db.CommunityEvents.Add(ev);

        // Host is automatically registered as Going
        var hostReg = EventRegistration.RegisterGoing(ev.Id, userId, "Host");
        _db.EventRegistrations.Add(hostReg);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount: 1, waitlistCount: 0));
    }

    public async Task<Result<CommunityEventDto>> GetEventByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var goingCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);

        var waitlistCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount, waitlistCount));
    }

    public async Task<Result<IReadOnlyList<CommunityEventDto>>> GetEventsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? category = null,
        string? postalCode = null,
        Guid? groupId = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.CommunityEvents
            .Where(e => !e.IsDeleted);

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.EndsAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(e => e.StartsAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(e => e.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            var post = postalCode.Trim();
            query = query.Where(e => e.LocationPostalCode == post);
        }

        if (groupId.HasValue)
        {
            query = query.Where(e => e.GroupId == groupId);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId);
        }

        var events = await query
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(cancellationToken);

        var eventIds = events.Select(e => e.Id).ToList();

        var goingCounts = await _db.EventRegistrations
            .Where(r => eventIds.Contains(r.EventId) && r.Status == EventRsvpStatus.Going)
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.EventId, v => v.Count, cancellationToken);

        var waitlistCounts = await _db.EventRegistrations
            .Where(r => eventIds.Contains(r.EventId) && r.Status == EventRsvpStatus.Waitlisted)
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.EventId, v => v.Count, cancellationToken);

        var dtos = events.Select(e => MapEvent(
            e,
            goingCounts.GetValueOrDefault(e.Id, 0),
            waitlistCounts.GetValueOrDefault(e.Id, 0))).ToList();

        return Result<IReadOnlyList<CommunityEventDto>>.Success(dtos);
    }

    public async Task<Result<EventRegistrationDto>> RegisterForEventAsync(
        Guid eventId,
        Guid userId,
        RegisterEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        if (ev.IsCancelled)
        {
            return Error.Validation("Cannot register for a cancelled event.");
        }

        var existing = await _db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, cancellationToken);

        if (existing is not null && existing.Status != EventRsvpStatus.Cancelled)
        {
            return Result<EventRegistrationDto>.Success(MapRegistration(existing));
        }

        EventRegistration registration;

        if (request.Status == EventRsvpStatus.Interested)
        {
            registration = EventRegistration.RegisterInterested(eventId, userId);
        }
        else
        {
            // Check capacity
            if (ev.Capacity.HasValue)
            {
                var goingCount = await _db.EventRegistrations
                    .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);

                if (goingCount >= ev.Capacity.Value)
                {
                    var waitlistCount = await _db.EventRegistrations
                        .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);

                    registration = EventRegistration.RegisterWaitlist(eventId, userId, waitlistCount + 1, request.Note);
                }
                else
                {
                    registration = EventRegistration.RegisterGoing(eventId, userId, request.Note);
                }
            }
            else
            {
                registration = EventRegistration.RegisterGoing(eventId, userId, request.Note);
            }
        }

        if (existing is not null)
        {
            _db.EventRegistrations.Remove(existing);
        }

        _db.EventRegistrations.Add(registration);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<EventRegistrationDto>.Success(MapRegistration(registration));
    }

    public async Task<Result> CancelRegistrationAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId && r.Status != EventRsvpStatus.Cancelled, cancellationToken);

        if (registration is null)
        {
            return Error.NotFound("EventRegistration");
        }

        var wasGoing = registration.Status == EventRsvpStatus.Going;
        registration.Cancel();

        // Auto-promote oldest waitlistee if a Going slot opened
        if (wasGoing)
        {
            var nextInLine = await _db.EventRegistrations
                .Where(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted)
                .OrderBy(r => r.WaitlistPosition)
                .ThenBy(r => r.RegisteredAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextInLine is not null)
            {
                nextInLine.PromoteToGoing();
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MyScheduleItemDto>>> GetMyScheduleAsync(
        Guid userId,
        DateTimeOffset? fromUtc = null,
        CancellationToken cancellationToken = default)
    {
        var threshold = fromUtc ?? DateTimeOffset.UtcNow.AddHours(-1);

        var registrations = await _db.EventRegistrations
            .Where(r => r.UserId == userId && r.Status != EventRsvpStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var eventIds = registrations.Select(r => r.EventId).Distinct().ToList();

        var events = await _db.CommunityEvents
            .Where(e => eventIds.Contains(e.Id) && !e.IsDeleted && e.EndsAtUtc >= threshold)
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(cancellationToken);

        var regMap = registrations.ToDictionary(r => r.EventId, r => r);

        var dtos = events.Select(e =>
        {
            var reg = regMap[e.Id];
            return new MyScheduleItemDto(
                EventId: e.Id,
                Title: e.Title,
                Category: e.Category,
                LocationAddress: e.LocationAddress,
                LocationPostalCode: e.LocationPostalCode,
                StartsAtUtc: e.StartsAtUtc,
                EndsAtUtc: e.EndsAtUtc,
                MyStatus: reg.Status,
                WaitlistPosition: reg.WaitlistPosition,
                IsCancelled: e.IsCancelled);
        }).ToList();

        return Result<IReadOnlyList<MyScheduleItemDto>>.Success(dtos);
    }

    // --- Contextual Threads ---

    public async Task<Result<MessageThreadDto>> GetOrCreateThreadAsync(
        ThreadContextType contextType,
        Guid contextId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _db.MessageThreads
            .FirstOrDefaultAsync(t => t.ContextType == contextType && t.ContextId == contextId, cancellationToken);

        if (thread is null)
        {
            thread = MessageThread.Create(contextType, contextId, "Discussion", userId);
            _db.MessageThreads.Add(thread);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<MessageThreadDto>.Success(MapThread(thread));
    }

    public async Task<Result<IReadOnlyList<ThreadMessageDto>>> GetMessagesAsync(
        Guid threadId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _db.ThreadMessages
            .Where(m => m.ThreadId == threadId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // BR-COMM-05 / ADR-020: a flagged message is held for a coordinator,
        // never silently shown to anyone but its own author while pending.
        var visible = messages.Where(m => !m.IsFlaggedForModeration || m.SenderUserId == requestingUserId);

        var dtos = visible.Select(MapMessage).ToList();
        return Result<IReadOnlyList<ThreadMessageDto>>.Success(dtos);
    }

    public async Task<Result<ThreadMessageDto>> PostMessageAsync(
        Guid threadId,
        Guid senderUserId,
        PostMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var thread = await _db.MessageThreads
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Error.NotFound("MessageThread");
        }

        if (thread.IsClosed)
        {
            return Error.Conflict("Thread is closed for new messages.");
        }

        var messageResult = ThreadMessage.Create(threadId, senderUserId, request.Content);
        if (messageResult.IsFailure)
        {
            return messageResult.Error!;
        }

        var message = messageResult.Value!;

        // BR-COMM-05 / ADR-020: screen before the message is visible to
        // anyone but its author. Held, never silently deleted.
        var verdict = _moderation.Screen(request.Content);
        if (verdict.IsFlagged)
        {
            message.FlagForModeration(verdict.Reason!);
        }

        _db.ThreadMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ThreadMessageDto>.Success(MapMessage(message));
    }

    public async Task<Result> DeleteMessageAsync(
        Guid threadId,
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.ThreadMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == threadId && !m.IsDeleted, cancellationToken);

        if (message is null)
        {
            return Error.NotFound("ThreadMessage");
        }

        if (message.SenderUserId != userId)
        {
            return Error.Forbidden("You can only delete your own messages.");
        }

        message.SoftDelete();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ThreadMessageDto>> ReportMessageAsync(
        Guid threadId,
        Guid messageId,
        Guid reporterUserId,
        ReportMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.ThreadMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == threadId && !m.IsDeleted, cancellationToken);

        if (message is null)
        {
            return Error.NotFound("ThreadMessage");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("Report reason cannot be empty.");
        }

        message.FlagForModeration(request.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
        return Result<ThreadMessageDto>.Success(MapMessage(message));
    }

    // --- Mapping Helpers ---

    private static CommunityGroupDto MapGroup(CommunityGroup g, int memberCount) => new(
        Id: g.Id,
        CreatorUserId: g.CreatorUserId,
        OrganizationId: g.OrganizationId,
        Title: g.Title,
        Description: g.Description,
        Category: g.Category,
        Scope: g.Scope,
        JoinPolicy: g.JoinPolicy,
        LocationPostalCode: g.LocationPostalCode,
        MaxMembers: g.MaxMembers,
        MemberCount: memberCount,
        IsArchived: g.IsArchived,
        CreatedAtUtc: g.CreatedAtUtc);

    private static GroupMembershipDto MapMembership(GroupMembership m) => new(
        Id: m.Id,
        GroupId: m.GroupId,
        UserId: m.UserId,
        Role: m.Role,
        Status: m.Status,
        JoinedAtUtc: m.JoinedAtUtc);

    private static CommunityEventDto MapEvent(CommunityEvent e, int goingCount, int waitlistCount) => new(
        Id: e.Id,
        HostUserId: e.HostUserId,
        GroupId: e.GroupId,
        OrganizationId: e.OrganizationId,
        Title: e.Title,
        Description: e.Description,
        Category: e.Category,
        LocationAddress: e.LocationAddress,
        LocationPostalCode: e.LocationPostalCode,
        StartsAtUtc: e.StartsAtUtc,
        EndsAtUtc: e.EndsAtUtc,
        RecurrenceFrequency: e.RecurrenceFrequency,
        RecurrenceUntilUtc: e.RecurrenceUntilUtc,
        Capacity: e.Capacity,
        GoingCount: goingCount,
        WaitlistCount: waitlistCount,
        IsCancelled: e.IsCancelled,
        CancellationReason: e.CancellationReason,
        CreatedAtUtc: e.CreatedAtUtc);

    private static EventRegistrationDto MapRegistration(EventRegistration r) => new(
        Id: r.Id,
        EventId: r.EventId,
        UserId: r.UserId,
        Status: r.Status,
        WaitlistPosition: r.WaitlistPosition,
        Note: r.Note,
        RegisteredAtUtc: r.RegisteredAtUtc);

    private static MessageThreadDto MapThread(MessageThread t) => new(
        Id: t.Id,
        ContextType: t.ContextType,
        ContextId: t.ContextId,
        Title: t.Title,
        IsClosed: t.IsClosed,
        CreatedAtUtc: t.CreatedAtUtc);

    private static ThreadMessageDto MapMessage(ThreadMessage m) => new(
        Id: m.Id,
        ThreadId: m.ThreadId,
        SenderUserId: m.SenderUserId,
        Content: m.Content,
        IsFlaggedForModeration: m.IsFlaggedForModeration,
        CreatedAtUtc: m.CreatedAtUtc);
}
