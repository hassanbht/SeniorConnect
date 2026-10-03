using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.Community.Infrastructure;

public sealed partial class CommunityService : ICommunityService
{
    private const int MinimumTrustLevelForGroupCreation = 1;

    /// <summary>P5-09: distinct reporters after which reported content is held for everyone.</summary>
    private const int DistinctReportersToHoldForEveryone = 3;

    /// <summary>Upper bound per item type in one moderation-queue read.</summary>
    private const int ModerationQueuePageSize = 100;

    private readonly ICommunityDbContext _db;
    private readonly ICommunityModerationService _moderation;
    private readonly IOrganizationCoordinatorReader _orgReader;
    private readonly IUserAccountStatusReader _accountStatusReader;
    private readonly ITrustLevelReader _trustLevelReader;
    private readonly ISafeguardingCaseReader _safeguardingCaseReader;
    private readonly ISafeguardingCaseRaiser? _safeguardingCaseRaiser;

    public CommunityService(
        ICommunityDbContext db,
        ICommunityModerationService moderation,
        IOrganizationCoordinatorReader orgReader,
        IUserAccountStatusReader accountStatusReader,
        ITrustLevelReader trustLevelReader,
        ISafeguardingCaseReader safeguardingCaseReader,
        ISafeguardingCaseRaiser? safeguardingCaseRaiser = null)
    {
        _db = db;
        _moderation = moderation;
        _orgReader = orgReader;
        _accountStatusReader = accountStatusReader;
        _trustLevelReader = trustLevelReader;
        _safeguardingCaseReader = safeguardingCaseReader;
        _safeguardingCaseRaiser = safeguardingCaseRaiser;
    }

    // --- Authorization helpers ---

    private async Task<Error?> RequireOrgStaffAsync(Guid? organizationId, Guid callerUserId, CancellationToken ct)
    {
        if (organizationId is null || organizationId == Guid.Empty)
        {
            return null;
        }

        var staffIds = await _orgReader.GetActiveCoordinatorUserIdsAsync(organizationId.Value, ct);
        if (!staffIds.Contains(callerUserId))
        {
            return Error.Forbidden("Only an active coordinator or admin of this organization may publish on its behalf.");
        }

        return null;
    }

    /// <summary>
    /// BR-COMM-06 / ADR-025: server-side eligibility gate for individual (non-organization)
    /// group creators. Never trust a client-sent eligibility flag.
    /// </summary>
    private async Task<Error?> RequireEligibleIndividualCreatorAsync(Guid userId, CancellationToken ct)
    {
        if (!await _accountStatusReader.IsActiveAsync(userId, ct))
        {
            return Error.Forbidden("Only active accounts may create a community group.");
        }

        var trustLevel = await _trustLevelReader.GetEffectiveTrustLevelAsync(userId, ct);
        if (trustLevel < MinimumTrustLevelForGroupCreation)
        {
            return Error.Forbidden("Contact verification is required before creating a community group.");
        }

        var isBannedFromAnyGroup = await _db.GroupMemberships
            .AnyAsync(m => m.UserId == userId && m.Status == GroupMembershipStatus.Banned, ct);
        if (isBannedFromAnyGroup)
        {
            return Error.Forbidden("Accounts with an active community ban cannot create new groups.");
        }

        if (await _safeguardingCaseReader.HasOpenCaseAsSubjectAsync(userId, ct))
        {
            return Error.Forbidden("Group creation is temporarily unavailable while a safeguarding case is open.");
        }

        return null;
    }

    private async Task<GroupMembership?> GetActiveMembershipAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        return await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.Status == GroupMembershipStatus.Active, ct);
    }

    private async Task<Error?> RequireGroupManagerAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        var membership = await GetActiveMembershipAsync(groupId, userId, ct);
        if (membership is null || (membership.Role != GroupMemberRole.Owner && membership.Role != GroupMemberRole.Admin && membership.Role != GroupMemberRole.Moderator))
        {
            return Error.Forbidden("Only group owners, admins or moderators can perform this action.");
        }

        return null;
    }

    private async Task<Error?> RequireGroupOwnerOrAdminAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        var membership = await GetActiveMembershipAsync(groupId, userId, ct);
        if (membership is null || (membership.Role != GroupMemberRole.Owner && membership.Role != GroupMemberRole.Admin))
        {
            return Error.Forbidden("Only group owners or admins can perform this action.");
        }

        return null;
    }

    private async Task<Error?> RequireGroupOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        var membership = await GetActiveMembershipAsync(groupId, userId, ct);
        if (membership?.Role != GroupMemberRole.Owner)
        {
            return Error.Forbidden("Only the group owner can perform this action.");
        }

        return null;
    }

    private async Task<bool> IsActiveMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        await _db.GroupMemberships
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.Status == GroupMembershipStatus.Active, ct);

    /// <summary>
    /// P5-06 / P5-09: an event inside a non-public group is visible only to that
    /// group's active members (and the host); an event held for review only to
    /// its host. Anything else is public.
    /// </summary>
    private async Task<bool> CanViewEventAsync(CommunityEvent ev, Guid? userId, CancellationToken ct)
    {
        if (userId is not null && ev.HostUserId == userId.Value)
        {
            return true;
        }

        if (ev.IsPendingReview)
        {
            return false;
        }

        if (ev.GroupId is not { } groupId)
        {
            return true;
        }

        var isPublicGroup = await _db.CommunityGroups
            .AnyAsync(g => g.Id == groupId && g.Visibility == GroupVisibility.Public, ct);

        return isPublicGroup || (userId is not null && await IsActiveMemberAsync(groupId, userId.Value, ct));
    }

    /// <summary>
    /// P5-03: who may edit / cancel an event. Organization events: active staff of
    /// that organization. Events without an organization: the host. In both
    /// cases a manager of the event's group may too. Callers that may not even
    /// see the event get 404.
    /// </summary>
    /// <summary>Phase 4: host, group manager or organization staff: sees a private venue's address.</summary>
    private async Task<bool> IsEventManagerAsync(CommunityEvent ev, Guid userId, CancellationToken ct) =>
        ev.HostUserId == userId || await RequireEventManagerAsync(ev, userId, ct) is null;

    private async Task<Error?> RequireEventManagerAsync(CommunityEvent ev, Guid userId, CancellationToken ct)
    {
        if (ev.OrganizationId is { } orgId && orgId != Guid.Empty)
        {
            var staffIds = await _orgReader.GetActiveCoordinatorUserIdsAsync(orgId, ct);
            if (staffIds.Contains(userId))
            {
                return null;
            }
        }
        else if (ev.HostUserId == userId)
        {
            return null;
        }

        if (ev.GroupId is { } groupId && await RequireGroupManagerAsync(groupId, userId, ct) is null)
        {
            return null;
        }

        if (!await CanViewEventAsync(ev, userId, ct))
        {
            return Error.NotFound("CommunityEvent");
        }

        return Error.Forbidden("Only the event host, a manager of its group or staff of its organization may change this event.");
    }

    /// <summary>
    /// P5-03b: who may publish a new event. Group events: a manager of that group
    /// (or staff of the group's organization). Organization events: staff of that
    /// organization, or a manager of a group that belongs to that same
    /// organization. Individual events: the ADR-025 individual eligibility gate.
    /// </summary>
    private async Task<Error?> RequireEventPublisherAsync(Guid userId, Guid? organizationId, Guid? groupId, CancellationToken ct)
    {
        var orgId = organizationId is { } o && o != Guid.Empty ? o : (Guid?)null;

        if (groupId is { } gid)
        {
            var group = await _db.CommunityGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gid, ct);
            if (group is null)
            {
                return Error.NotFound("CommunityGroup");
            }

            if (orgId is not null && group.OrganizationId != orgId)
            {
                return Error.Validation("The group does not belong to the given organization.");
            }

            if (await RequireGroupManagerAsync(gid, userId, ct) is null)
            {
                return null;
            }

            var groupOrgId = orgId ?? group.OrganizationId;
            return groupOrgId is not null && await RequireOrgStaffAsync(groupOrgId, userId, ct) is null
                ? null
                : Error.Forbidden("Only group owners, admins or moderators can publish events in this group.");
        }

        if (orgId is not null)
        {
            return await RequireOrgStaffAsync(orgId, userId, ct);
        }

        return await RequireEligibleIndividualCreatorAsync(userId, ct);
    }

    /// <summary>
    /// P5-02: a manager may only act on members they strictly outrank
    /// (Owner &gt; Admin &gt; Moderator &gt; Member) — a moderator can't ban an admin.
    /// </summary>
    private async Task<Error?> RequireOutranksAsync(Guid groupId, Guid actorUserId, GroupMembership target, CancellationToken ct)
    {
        var actor = await GetActiveMembershipAsync(groupId, actorUserId, ct);
        return actor is not null && actor.Role < target.Role
            ? null
            : Error.Forbidden("You can only moderate members with a lower group role than your own.");
    }

    private static bool IsManagerRole(GroupMemberRole role) =>
        role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Moderator;

    /// <summary>BR-COMM-05: returns the hold reason, or null when the text may be published.</summary>
    private async Task<string?> ScreenAsync(string contentType, Guid authorUserId, Guid? groupId, CancellationToken ct, params string?[] parts)
    {
        var text = string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var verdict = await _moderation.EvaluateAsync(new ModerationContext(contentType, text, authorUserId, groupId), ct);
        return verdict.Decision == ModerationDecision.Allow ? null : verdict.Reason ?? "Inhalt wird geprüft.";
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };

    private static Error ConcurrencyConflict() => new(
        "CONCURRENCY_CONFLICT",
        "This item was changed by someone else at the same time. Please try again.",
        ErrorKind.Conflict);

    /// <summary>Saves; maps a lost xmin race to a clean 409 instead of a 500.</summary>
    private async Task<Error?> SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
    }

    private async Task<Error?> RequireThreadAccessAsync(ThreadContextType contextType, Guid contextId, Guid userId, CancellationToken ct)
    {
        if (contextType == ThreadContextType.Event)
        {
            var ev = await _db.CommunityEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == contextId, ct);
            return ev is not null && await CanViewEventAsync(ev, userId, ct)
                ? null
                : Error.NotFound("CommunityEvent");
        }

        var group = await _db.CommunityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == contextId, ct);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        if (group.Visibility == GroupVisibility.Public)
        {
            return null;
        }

        var isActiveMember = await _db.GroupMemberships
            .AnyAsync(m => m.GroupId == group.Id && m.UserId == userId && m.Status == GroupMembershipStatus.Active, ct);

        return isActiveMember
            ? null
            : Error.Forbidden("Only active members of this group may access its discussion thread.");
    }

    // --- Groups ---

    public async Task<Result<CommunityGroupDto>> CreateGroupAsync(
        Guid userId,
        CreatorType creatorType,
        CreateCommunityGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireOrgStaffAsync(request.OrganizationId, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        if (request.OrganizationId is null || request.OrganizationId == Guid.Empty)
        {
            var eligibilityError = await RequireEligibleIndividualCreatorAsync(userId, cancellationToken);
            if (eligibilityError is not null)
            {
                return eligibilityError;
            }
        }

        var verificationStatus = GroupVerificationStatus.Unverified;
        if (creatorType == CreatorType.VerifiedOrganization)
        {
            verificationStatus = GroupVerificationStatus.OrganizationVerified;
        }

        var groupResult = CommunityGroup.Create(
            creatorUserId: userId,
            creatorType: creatorType,
            name: request.Name,
            description: request.Description,
            type: request.Type,
            visibility: request.Visibility,
            organizationId: request.OrganizationId,
            city: request.City,
            region: request.Region,
            postalCode: request.PostalCode,
            latitude: request.Latitude,
            longitude: request.Longitude,
            primaryLanguageCode: request.PrimaryLanguageCode,
            coverImageUrl: request.CoverImageUrl,
            rules: request.Rules,
            maxMembers: request.MaxMembers,
            verificationStatus: verificationStatus);

        if (groupResult.IsFailure)
        {
            return groupResult.Error!;
        }

        var group = groupResult.Value!;
        if (await ScreenAsync("group_description", userId, null, cancellationToken, group.Name, group.Description, group.Rules) is not null)
        {
            group.HoldForReview();
        }

        _db.CommunityGroups.Add(group);

        // Creator is automatic Owner
        var ownerMembership = GroupMembership.CreateOwner(group.Id, userId);
        _db.GroupMemberships.Add(ownerMembership);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount: 1));
    }

    public async Task<Result<CommunityGroupDto>> GetGroupByIdAsync(
        Guid groupId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var memberCount = await _db.GroupMemberships
            .CountAsync(m => m.GroupId == groupId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        GroupMembershipStatus? myStatus = null;
        GroupMemberRole? myRole = null;
        if (requestingUserId.HasValue)
        {
            var myMembership = await _db.GroupMemberships
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == requestingUserId.Value, cancellationToken);

            if (myMembership is not null)
            {
                myStatus = myMembership.Status;
                myRole = myMembership.Role;
            }
        }

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount, myStatus, myRole));
    }

    public async Task<Result<IReadOnlyList<CommunityGroupDto>>> GetGroupsAsync(
        string? category = null,
        string? postalCode = null,
        string? city = null,
        string? region = null,
        string? language = null,
        GroupType? type = null,
        Guid? organizationId = null,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.CommunityGroups
            .AsNoTracking()
            .Where(g => !g.IsDeleted && g.Status != GroupStatus.Archived && g.Status != GroupStatus.Blocked && g.Status != GroupStatus.Draft);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            q = q.Where(g => g.Category == cat);
        }

        if (type.HasValue)
        {
            q = q.Where(g => g.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            var post = postalCode.Trim();
            q = q.Where(g => g.PostalCode == post);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim();
            q = q.Where(g => g.City != null && EF.Functions.ILike(g.City, $"%{c}%"));
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var r = region.Trim();
            q = q.Where(g => g.Region != null && EF.Functions.ILike(g.Region, $"%{r}%"));
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            var lang = language.Trim().ToLowerInvariant();
            q = q.Where(g => g.PrimaryLanguageCode == lang);
        }

        if (organizationId.HasValue)
        {
            q = q.Where(g => g.OrganizationId == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            q = q.Where(g =>
                EF.Functions.ILike(g.Name, $"%{search}%") ||
                (g.Description != null && EF.Functions.ILike(g.Description, $"%{search}%")));
        }

        var groups = await q
            .OrderByDescending(g => g.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();

        var memberCounts = await _db.GroupMemberships
            .AsNoTracking()
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

        var authError = await RequireGroupOwnerOrAdminAsync(groupId, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var updateResult = group.Update(
            name: request.Name,
            description: request.Description,
            type: request.Type,
            visibility: request.Visibility,
            city: request.City,
            region: request.Region,
            postalCode: request.PostalCode,
            latitude: request.Latitude,
            longitude: request.Longitude,
            primaryLanguageCode: request.PrimaryLanguageCode,
            coverImageUrl: request.CoverImageUrl,
            rules: request.Rules,
            maxMembers: request.MaxMembers,
            updatedByUserId: userId);

        if (updateResult.IsFailure)
        {
            return updateResult.Error!;
        }

        if (await ScreenAsync("group_description", userId, groupId, cancellationToken, group.Name, group.Description, group.Rules) is not null)
        {
            group.HoldForReview();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var memberCount = await _db.GroupMemberships
            .CountAsync(m => m.GroupId == groupId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        return Result<CommunityGroupDto>.Success(MapGroup(group, memberCount));
    }

    public async Task<Result> ArchiveGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var authError = await RequireGroupOwnerAsync(groupId, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        group.Archive(userId);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // --- Membership ---

    public async Task<Result<GroupMembershipDto>> JoinGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        if (group.Status != GroupStatus.Active)
        {
            return Error.Conflict("This group is not currently accepting members.");
        }

        var existing = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancellationToken);

        switch (existing?.Status)
        {
            case GroupMembershipStatus.Active:
                return Error.Conflict("ALREADY_MEMBER", "You are already an active member of this group.");
            case GroupMembershipStatus.Muted:
                // P5-02: re-joining must never lift a mute.
                return Error.Conflict("MEMBERSHIP_MUTED", "You are already a member of this group and are currently muted.");
            case GroupMembershipStatus.Pending:
                return Error.Conflict("MEMBERSHIP_PENDING", "Your membership request is already pending approval.");
            case GroupMembershipStatus.Banned:
                return new Error("MEMBERSHIP_BANNED", "You are banned from this group.", ErrorKind.Forbidden);
            case GroupMembershipStatus.Removed:
                return new Error(
                    "MEMBERSHIP_REMOVED",
                    "You were removed from this group. A group admin can invite you again.",
                    ErrorKind.Forbidden);
        }

        var requiresApproval = group.Visibility != GroupVisibility.Public;
        if (!requiresApproval)
        {
            var fullError = await RequireGroupHasRoomAsync(group, cancellationToken);
            if (fullError is not null)
            {
                return fullError;
            }
        }

        GroupMembership membership;
        if (existing is not null)
        {
            // Left / Rejected: reopen the same row (unique (group, user)).
            var rejoinResult = existing.Rejoin(requiresApproval, userId);
            if (rejoinResult.IsFailure)
            {
                return rejoinResult.Error!;
            }

            membership = existing;
        }
        else
        {
            membership = requiresApproval
                ? GroupMembership.RequestToJoin(groupId, userId)
                : GroupMembership.JoinDirectly(groupId, userId);
            _db.GroupMemberships.Add(membership);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Double tap: the other request already created the row.
            return Error.Conflict("ALREADY_MEMBER", "You are already a member of this group or your request is pending.");
        }

        return Result<GroupMembershipDto>.Success(MapMembership(membership));
    }

    // ponytail: check-then-insert — two simultaneous joins can overshoot MaxMembers by one;
    // add an xmin bump on the group (like events) if MaxMembers becomes a hard limit.
    private async Task<Error?> RequireGroupHasRoomAsync(CommunityGroup group, CancellationToken ct)
    {
        if (group.MaxMembers is not { } max)
        {
            return null;
        }

        var memberCount = await _db.GroupMemberships.CountAsync(
            m => m.GroupId == group.Id && (m.Status == GroupMembershipStatus.Active || m.Status == GroupMembershipStatus.Muted), ct);

        return memberCount >= max
            ? Error.Conflict("GROUP_FULL", "This group has reached its maximum number of members.")
            : null;
    }

    public async Task<Result<GroupMembershipDto>> AcceptInvitationAsync(
        Guid groupId,
        string token,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _db.CommunityInvitations
            .FirstOrDefaultAsync(i => i.GroupId == groupId && i.Token == token && i.Status == CommunityInvitationStatus.Pending, cancellationToken);

        if (invitation is null)
        {
            return Error.NotFound("CommunityInvitation");
        }

        if (invitation.InvitedUserId.HasValue && invitation.InvitedUserId.Value != userId)
        {
            return Error.Forbidden("This invitation is for another user.");
        }

        var existing = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancellationToken);

        if (existing?.Status is GroupMembershipStatus.Active or GroupMembershipStatus.Muted)
        {
            return Error.Conflict("ALREADY_MEMBER", "You are already a member of this group.");
        }

        if (existing?.Status == GroupMembershipStatus.Banned)
        {
            return new Error("MEMBERSHIP_BANNED", "You are banned from this group.", ErrorKind.Forbidden);
        }

        var group = await _db.CommunityGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var fullError = await RequireGroupHasRoomAsync(group, cancellationToken);
        if (fullError is not null)
        {
            return fullError;
        }

        var acceptResult = invitation.Accept(userId);
        if (acceptResult.IsFailure)
        {
            await _db.SaveChangesAsync(cancellationToken); // persist Expired status transition, if any
            return acceptResult.Error!;
        }

        GroupMembership membership;
        if (existing is not null)
        {
            // An admin's invitation may reopen a Left / Rejected / Removed / Pending row.
            var rejoinResult = existing.Rejoin(requiresApproval: false, userId);
            if (rejoinResult.IsFailure)
            {
                return rejoinResult.Error!;
            }

            membership = existing;
        }
        else
        {
            membership = GroupMembership.JoinDirectly(groupId, userId);
            _db.GroupMemberships.Add(membership);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<GroupMembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result> ApproveMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupOwnerOrAdminAsync(groupId, adminUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var targetMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (targetMembership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var group = await _db.CommunityGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        var fullError = group is null ? Error.NotFound("CommunityGroup") : await RequireGroupHasRoomAsync(group, cancellationToken);
        if (fullError is not null)
        {
            return fullError;
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
        var authError = await RequireGroupOwnerOrAdminAsync(groupId, adminUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
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

    public async Task<Result> RemoveMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupOwnerOrAdminAsync(groupId, adminUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        if (membership.Role == GroupMemberRole.Owner)
        {
            return Error.Forbidden("Cannot remove the group owner. Transfer ownership first.");
        }

        var rankError = await RequireOutranksAsync(groupId, adminUserId, membership, cancellationToken);
        if (rankError is not null)
        {
            return rankError;
        }

        membership.Remove(adminUserId);

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: adminUserId,
            action: CommunityModerationActionType.RemoveMember,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: reason,
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MuteMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        DateTimeOffset untilUtc,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        if (membership.Role == GroupMemberRole.Owner)
        {
            return Error.Forbidden("Cannot mute the group owner.");
        }

        var rankError = await RequireOutranksAsync(groupId, moderatorUserId, membership, cancellationToken);
        if (rankError is not null)
        {
            return rankError;
        }

        var muteResult = membership.Mute(moderatorUserId, untilUtc, reason);
        if (muteResult.IsFailure)
        {
            return muteResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.MuteMember,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: reason ?? "Muted",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnmuteMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var rankError = await RequireOutranksAsync(groupId, moderatorUserId, membership, cancellationToken);
        if (rankError is not null)
        {
            return rankError;
        }

        var unmuteResult = membership.Unmute(moderatorUserId);
        if (unmuteResult.IsFailure)
        {
            return unmuteResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.UnmuteMember,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: "Unmuted",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> BanMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        if (membership.Role == GroupMemberRole.Owner)
        {
            return Error.Forbidden("Cannot ban the group owner.");
        }

        var rankError = await RequireOutranksAsync(groupId, moderatorUserId, membership, cancellationToken);
        if (rankError is not null)
        {
            return rankError;
        }

        var banResult = membership.Ban(moderatorUserId, reason);
        if (banResult.IsFailure)
        {
            return banResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.BanMember,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: reason,
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnbanMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var rankError = await RequireOutranksAsync(groupId, moderatorUserId, membership, cancellationToken);
        if (rankError is not null)
        {
            return rankError;
        }

        var unbanResult = membership.Unban(moderatorUserId);
        if (unbanResult.IsFailure)
        {
            return unbanResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.UnbanMember,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: "Unbanned",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<CommunityInvitationDto>> InviteMemberAsync(
        Guid groupId,
        Guid invitedByUserId,
        Guid? invitedUserId,
        string? email,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupOwnerOrAdminAsync(groupId, invitedByUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var invitationResult = CommunityInvitation.Create(
            groupId: groupId,
            invitedByUserId: invitedByUserId,
            invitedUserId: invitedUserId,
            email: email,
            phone: phone);

        if (invitationResult.IsFailure)
        {
            return invitationResult.Error!;
        }

        _db.CommunityInvitations.Add(invitationResult.Value!);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityInvitationDto>.Success(MapInvitation(invitationResult.Value!));
    }

    public async Task<Result> AppointModeratorAsync(
        Guid groupId,
        Guid targetUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupOwnerAsync(groupId, ownerUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("GroupMembership");
        }

        var promoteResult = membership.Promote(GroupMemberRole.Moderator, ownerUserId);
        if (promoteResult.IsFailure)
        {
            return promoteResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: ownerUserId,
            action: CommunityModerationActionType.AppointModerator,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: "Appointed as moderator",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveModeratorAsync(
        Guid groupId,
        Guid targetUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupOwnerAsync(groupId, ownerUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == targetUserId && m.Status == GroupMembershipStatus.Active, cancellationToken);

        if (membership is null || membership.Role != GroupMemberRole.Moderator)
        {
            return Error.NotFound("GroupMembership");
        }

        var promoteResult = membership.Promote(GroupMemberRole.Member, ownerUserId);
        if (promoteResult.IsFailure)
        {
            return promoteResult.Error!;
        }

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: ownerUserId,
            action: CommunityModerationActionType.RemoveModerator,
            targetType: CommunityModerationTargetType.Member,
            targetId: targetUserId,
            reason: "Removed as moderator",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupMembershipDto>>> GetGroupMembersAsync(
        Guid groupId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        // P5-02b: same rule as the group's discussion thread — a non-public
        // group's member list is for its active members only.
        var accessError = await RequireThreadAccessAsync(ThreadContextType.Group, groupId, requestingUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var members = await _db.GroupMemberships
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && (m.Status == GroupMembershipStatus.Active || m.Status == GroupMembershipStatus.Muted))
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<GroupMembershipDto>>.Success(members.Select(MapMembership).ToList());
    }

    // --- Events (existing, updated) ---

    public async Task<Result<CommunityEventDto>> CreateEventAsync(
        Guid userId,
        CreateCommunityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireEventPublisherAsync(userId, request.OrganizationId, request.GroupId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

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
            capacity: request.Capacity,
            venueType: request.VenueType);

        if (eventResult.IsFailure)
        {
            return eventResult.Error!;
        }

        var ev = eventResult.Value!;
        if (await ScreenAsync("event_description", userId, ev.GroupId, cancellationToken, ev.Title, ev.Description) is not null)
        {
            ev.HoldForReview();
        }

        _db.CommunityEvents.Add(ev);

        var hostReg = EventRegistration.RegisterGoing(ev.Id, userId, "Host");
        _db.EventRegistrations.Add(hostReg);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount: 1, waitlistCount: 0, isManager: true));
    }

    public async Task<Result<CommunityEventDto>> GetEventByIdAsync(
        Guid eventId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null || !await CanViewEventAsync(ev, requestingUserId, cancellationToken))
        {
            return Error.NotFound("CommunityEvent");
        }

        var goingCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);

        var waitlistCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);

        var cancelledOccurrences = await _db.EventOccurrenceCancellations
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => c.OccurrenceStartUtc)
            .ToListAsync(cancellationToken);

        EventRsvpStatus? myStatus = null;
        if (requestingUserId is not null)
        {
            var myRegistration = await _db.EventRegistrations
                .AsNoTracking()
                .Where(r => r.EventId == eventId && r.UserId == requestingUserId.Value && r.Status != EventRsvpStatus.Cancelled)
                .Select(r => (EventRsvpStatus?)r.Status)
                .FirstOrDefaultAsync(cancellationToken);
            myStatus = myRegistration;
        }

        return Result<CommunityEventDto>.Success(MapEvent(
            ev, goingCount, waitlistCount, cancelledOccurrences, myStatus,
            isManager: requestingUserId is { } viewer && await IsEventManagerAsync(ev, viewer, cancellationToken)));
    }

    public async Task<Result<IReadOnlyList<CommunityEventDto>>> GetEventsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? category = null,
        string? postalCode = null,
        Guid? groupId = null,
        Guid? organizationId = null,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var uid = requestingUserId ?? Guid.Empty;
        var publicGroupIds = _db.CommunityGroups
            .Where(g => g.Visibility == GroupVisibility.Public)
            .Select(g => g.Id);
        var myGroupIds = _db.GroupMemberships
            .Where(m => m.UserId == uid && m.Status == GroupMembershipStatus.Active)
            .Select(m => m.GroupId);

        // P5-06: private-group events only for that group's members; held events only for the host.
        var query = _db.CommunityEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Where(e => e.HostUserId == uid
                || (!e.IsPendingReview
                    && (e.GroupId == null
                        || publicGroupIds.Contains(e.GroupId.Value)
                        || myGroupIds.Contains(e.GroupId.Value))));

        if (fromUtc.HasValue)
        {
            // P5-04: a recurring series stays listed until its recurrence window ends.
            query = query.Where(CommunityEvent.IsRunningOnOrAfter(fromUtc.Value));
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
            .AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId) && r.Status == EventRsvpStatus.Going)
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.EventId, v => v.Count, cancellationToken);

        var waitlistCounts = await _db.EventRegistrations
            .AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId) && r.Status == EventRsvpStatus.Waitlisted)
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.EventId, v => v.Count, cancellationToken);

        var cancelledOccurrencesByEvent = await _db.EventOccurrenceCancellations
            .AsNoTracking()
            .Where(c => eventIds.Contains(c.EventId))
            .GroupBy(c => c.EventId)
            .Select(g => new { EventId = g.Key, Starts = g.Select(c => c.OccurrenceStartUtc).ToList() })
            .ToDictionaryAsync(k => k.EventId, v => (IReadOnlyCollection<DateTimeOffset>)v.Starts, cancellationToken);

        var dtos = events.Select(e => MapEvent(
            e,
            goingCounts.GetValueOrDefault(e.Id, 0),
            waitlistCounts.GetValueOrDefault(e.Id, 0),
            cancelledOccurrencesByEvent.GetValueOrDefault(e.Id),
            isManager: e.HostUserId == uid)).ToList();

        return Result<IReadOnlyList<CommunityEventDto>>.Success(dtos);
    }

    public async Task<Result<CommunityEventDto>> UpdateEventAsync(
        Guid eventId,
        Guid userId,
        UpdateCommunityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var authError = await RequireEventManagerAsync(ev, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var updateResult = ev.UpdateDetails(
            title: request.Title,
            description: request.Description,
            category: request.Category,
            locationAddress: request.LocationAddress,
            locationPostalCode: request.LocationPostalCode,
            capacity: request.Capacity,
            updatedByUserId: userId,
            venueType: request.VenueType);

        if (updateResult.IsFailure)
        {
            return updateResult.Error!;
        }

        if (await ScreenAsync("event_description", userId, ev.GroupId, cancellationToken, ev.Title, ev.Description) is not null)
        {
            ev.HoldForReview();
        }

        var saveError = await SaveOrConflictAsync(cancellationToken);
        if (saveError is not null)
        {
            return saveError;
        }

        var goingCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);
        var waitlistCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);
        var cancelledOccurrences = await _db.EventOccurrenceCancellations
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => c.OccurrenceStartUtc)
            .ToListAsync(cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount, waitlistCount, cancelledOccurrences, isManager: true));
    }

    public async Task<Result> CancelEventAsync(
        Guid eventId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var authError = await RequireEventManagerAsync(ev, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var cancelResult = ev.Cancel(reason, userId);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error!;
        }

        var saveError = await SaveOrConflictAsync(cancellationToken);
        return saveError is null ? Result.Success() : saveError;
    }

    public async Task<Result<CommunityEventDto>> CancelEventOccurrenceAsync(
        Guid eventId,
        Guid userId,
        CancelEventOccurrenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var authError = await RequireEventManagerAsync(ev, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        if (ev.RecurrenceFrequency == EventRecurrenceFrequency.None)
        {
            return Error.Validation("This event has no individual occurrences — cancel the event itself instead.");
        }

        var validOccurrences = ev.ComputeOccurrenceStartsUtc();
        if (!validOccurrences.Contains(request.OccurrenceStartUtc))
        {
            return Error.Validation("The given date is not an occurrence of this event's series.");
        }

        var alreadyCancelled = await _db.EventOccurrenceCancellations
            .AnyAsync(c => c.EventId == eventId && c.OccurrenceStartUtc == request.OccurrenceStartUtc, cancellationToken);
        if (alreadyCancelled)
        {
            return Error.Conflict("This occurrence is already cancelled.");
        }

        var cancellationResult = CommunityEventOccurrenceCancellation.Create(
            eventId, request.OccurrenceStartUtc, request.Reason, userId);
        if (cancellationResult.IsFailure)
        {
            return cancellationResult.Error!;
        }

        _db.EventOccurrenceCancellations.Add(cancellationResult.Value!);
        await _db.SaveChangesAsync(cancellationToken);

        var goingCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);
        var waitlistCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);
        var cancelledOccurrences = await _db.EventOccurrenceCancellations
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => c.OccurrenceStartUtc)
            .ToListAsync(cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount, waitlistCount, cancelledOccurrences, isManager: true));
    }

    public async Task<Result<EventRegistrationDto>> RegisterForEventAsync(
        Guid eventId,
        Guid userId,
        RegisterEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null || !await CanViewEventAsync(ev, userId, cancellationToken))
        {
            return Error.NotFound("CommunityEvent");
        }

        if (ev.IsCancelled)
        {
            return Error.Validation("Cannot register for a cancelled event.");
        }

        if (ev.HasEndedBy(DateTimeOffset.UtcNow))
        {
            return new Error("EVENT_ENDED", "This event is already over.", ErrorKind.Validation);
        }

        var existing = await _db.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, cancellationToken);

        if (existing is not null && existing.Status != EventRsvpStatus.Cancelled)
        {
            return Result<EventRegistrationDto>.Success(MapRegistration(existing));
        }

        var status = EventRsvpStatus.Going;
        int? waitlistPosition = null;

        if (request.Status == EventRsvpStatus.Interested)
        {
            status = EventRsvpStatus.Interested;
        }
        else if (ev.Capacity.HasValue)
        {
            // P5-05: bump the event row so the xmin token serialises concurrent
            // capacity decisions — the loser of a race gets a conflict, not a seat.
            ev.TouchForCapacityCheck();

            var goingCount = await _db.EventRegistrations
                .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);

            if (goingCount >= ev.Capacity.Value)
            {
                status = EventRsvpStatus.Waitlisted;
                waitlistPosition = await _db.EventRegistrations
                    .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken) + 1;
            }
        }

        EventRegistration registration;
        if (existing is not null)
        {
            existing.Reopen(status, waitlistPosition, request.Note);
            registration = existing;
        }
        else
        {
            registration = status switch
            {
                EventRsvpStatus.Interested => EventRegistration.RegisterInterested(eventId, userId),
                EventRsvpStatus.Waitlisted => EventRegistration.RegisterWaitlist(eventId, userId, waitlistPosition!.Value, request.Note),
                _ => EventRegistration.RegisterGoing(eventId, userId, request.Note),
            };
            _db.EventRegistrations.Add(registration);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
        {
            // Double tap / concurrent RSVP: if the other request registered this
            // user, answer idempotently with that row; otherwise ask to retry.
            var winner = await _db.EventRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId && r.Status != EventRsvpStatus.Cancelled, cancellationToken);

            return winner is not null
                ? Result<EventRegistrationDto>.Success(MapRegistration(winner))
                : ConcurrencyConflict();
        }

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

                // Promotion is a capacity decision too — serialise it with new RSVPs.
                var ev = await _db.CommunityEvents.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
                ev?.TouchForCapacityCheck();
            }
        }

        var saveError = await SaveOrConflictAsync(cancellationToken);
        return saveError is null ? Result.Success() : saveError;
    }

    public async Task<Result<IReadOnlyList<MyScheduleItemDto>>> GetMyScheduleAsync(
        Guid userId,
        DateTimeOffset? fromUtc = null,
        CancellationToken cancellationToken = default)
    {
        var threshold = fromUtc ?? DateTimeOffset.UtcNow.AddHours(-1);

        var registrations = await _db.EventRegistrations
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Status != EventRsvpStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var eventIds = registrations.Select(r => r.EventId).Distinct().ToList();

        var events = await _db.CommunityEvents
            .AsNoTracking()
            .Where(e => eventIds.Contains(e.Id) && !e.IsDeleted)
            .Where(CommunityEvent.IsRunningOnOrAfter(threshold))
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(cancellationToken);

        // Robust to legacy duplicate rows (pre unique index): newest RSVP wins.
        var regMap = registrations
            .GroupBy(r => r.EventId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RegisteredAtUtc).First());

        var dtos = events.Select(e =>
        {
            var reg = regMap[e.Id];
            return new MyScheduleItemDto(
                EventId: e.Id,
                Title: e.Title,
                Category: e.Category,
                LocationAddress: e.RevealsAddressTo(e.HostUserId == userId, reg.Status) ? e.LocationAddress : null,
                LocationPostalCode: e.LocationPostalCode,
                StartsAtUtc: e.StartsAtUtc,
                EndsAtUtc: e.EndsAtUtc,
                MyStatus: reg.Status,
                WaitlistPosition: reg.WaitlistPosition,
                IsCancelled: e.IsCancelled);
        }).ToList();

        return Result<IReadOnlyList<MyScheduleItemDto>>.Success(dtos);
    }

    // --- Posts / Announcements ---

    public async Task<Result<CommunityPostDto>> CreatePostAsync(
        Guid groupId,
        Guid authorUserId,
        CreateCommunityPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var membership = await GetActiveMembershipAsync(groupId, authorUserId, cancellationToken);
        if (membership is null)
        {
            return Error.Forbidden("Only active members can post.");
        }

        if (request.PostType == CommunityPostType.Announcement && membership.Role != GroupMemberRole.Owner && membership.Role != GroupMemberRole.Admin && membership.Role != GroupMemberRole.Moderator)
        {
            return Error.Forbidden("Only group managers can publish announcements.");
        }

        if (request.IsPinned && membership.Role != GroupMemberRole.Owner && membership.Role != GroupMemberRole.Admin && membership.Role != GroupMemberRole.Moderator)
        {
            return Error.Forbidden("Only group managers can pin posts.");
        }

        var postResult = CommunityPost.Create(
            authorUserId: authorUserId,
            groupId: groupId,
            postType: request.PostType,
            title: request.Title,
            content: request.Content,
            isPinned: request.IsPinned);

        if (postResult.IsFailure)
        {
            return postResult.Error!;
        }

        var post = postResult.Value!;

        // P4-18 / BR-COMM-05: the title is published too, so it is screened too.
        var holdReason = await ScreenAsync(
            request.PostType == CommunityPostType.Announcement ? "announcement" : "post",
            authorUserId, groupId, cancellationToken, request.Title, request.Content);

        if (holdReason is not null)
        {
            post.SetPendingReview(holdReason);
        }

        _db.CommunityPosts.Add(post);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<CommunityPostDto>.Success(MapPost(post));
    }

    public async Task<Result<IReadOnlyList<CommunityPostDto>>> GetGroupPostsAsync(
        Guid groupId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.CommunityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        if (group.Visibility != GroupVisibility.Public)
        {
            if (!requestingUserId.HasValue)
            {
                return Error.Forbidden("This group is private.");
            }

            var isMember = await _db.GroupMemberships
                .AnyAsync(m => m.GroupId == groupId && m.UserId == requestingUserId.Value && m.Status == GroupMembershipStatus.Active, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("Only members can view this group's posts.");
            }
        }

        var posts = await _db.CommunityPosts
            .AsNoTracking()
            .Where(p => p.GroupId == groupId && !p.IsDeleted && p.Status != CommunityPostStatus.Removed)
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var reportedByMe = await ReportedTargetIdsAsync(requestingUserId, CommunityReportTargetType.Post, posts.Select(p => p.Id), cancellationToken);

        var visible = posts
            .Where(p => p.AuthorUserId == requestingUserId
                || (p.Status == CommunityPostStatus.Visible && !reportedByMe.Contains(p.Id)))
            .ToList();
        return Result<IReadOnlyList<CommunityPostDto>>.Success(visible.Select(MapPost).ToList());
    }

    public async Task<Result<CommunityPostDto>> HidePostAsync(
        Guid groupId,
        Guid postId,
        Guid moderatorUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var post = await _db.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == postId && p.GroupId == groupId && !p.IsDeleted, cancellationToken);

        if (post is null)
        {
            return Error.NotFound("CommunityPost");
        }

        post.Hide(moderatorUserId, reason);

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.HidePost,
            targetType: CommunityModerationTargetType.Post,
            targetId: postId,
            reason: reason ?? "Hidden by moderator",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<CommunityPostDto>.Success(MapPost(post));
    }

    public async Task<Result<CommunityPostDto>> RemovePostAsync(
        Guid groupId,
        Guid postId,
        Guid moderatorUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var post = await _db.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == postId && p.GroupId == groupId && !p.IsDeleted, cancellationToken);

        if (post is null)
        {
            return Error.NotFound("CommunityPost");
        }

        // Authors can remove their own posts; managers can remove any post.
        if (post.AuthorUserId != moderatorUserId)
        {
            var authError = await RequireGroupManagerAsync(groupId, moderatorUserId, cancellationToken);
            if (authError is not null)
            {
                return authError;
            }
        }

        post.Remove(moderatorUserId, reason);

        _db.CommunityModerationActions.Add(CommunityModerationAction.Create(
            actorUserId: moderatorUserId,
            action: CommunityModerationActionType.RemovePost,
            targetType: CommunityModerationTargetType.Post,
            targetId: postId,
            reason: reason ?? "Removed",
            groupId: groupId));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<CommunityPostDto>.Success(MapPost(post));
    }

    // --- Reports ---

    public async Task<Result<CommunityReportDto>> CreateReportAsync(
        Guid reporterUserId,
        CreateCommunityReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveReportTargetAsync(request.TargetType, request.TargetId, request.GroupId, reporterUserId, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error!;
        }

        return await FileReportAsync(
            reporterUserId, request.TargetType, request.TargetId, request.Category, request.Description, target.Value, cancellationToken);
    }

    /// <summary>
    /// P5-09: the target must exist and be visible to the reporter (404
    /// otherwise). The report's group — which decides which group managers
    /// review it — is derived server-side from the target, never taken from
    /// the client, except to narrow a Member report to one group.
    /// </summary>
    private async Task<Result<Guid?>> ResolveReportTargetAsync(
        CommunityReportTargetType targetType, Guid targetId, Guid? claimedGroupId, Guid reporterUserId, CancellationToken ct)
    {
        switch (targetType)
        {
            case CommunityReportTargetType.Group:
                return await _db.CommunityGroups.AnyAsync(g => g.Id == targetId, ct)
                    ? (Guid?)targetId
                    : Error.NotFound("CommunityGroup");

            case CommunityReportTargetType.Member:
                var isMember = await _db.GroupMemberships
                    .AnyAsync(m => m.UserId == targetId && (claimedGroupId == null || m.GroupId == claimedGroupId), ct);
                return isMember ? claimedGroupId : Error.NotFound("GroupMembership");

            case CommunityReportTargetType.Post:
                var postGroupId = await _db.CommunityPosts
                    .Where(p => p.Id == targetId)
                    .Select(p => (Guid?)p.GroupId)
                    .FirstOrDefaultAsync(ct);
                return await VisibleGroupOrNotFoundAsync(postGroupId, reporterUserId, "CommunityPost", ct);

            case CommunityReportTargetType.Comment:
                var commentGroupId = await _db.CommunityComments
                    .Where(c => c.Id == targetId)
                    .Join(_db.CommunityPosts, c => c.PostId, p => p.Id, (c, p) => (Guid?)p.GroupId)
                    .FirstOrDefaultAsync(ct);
                return await VisibleGroupOrNotFoundAsync(commentGroupId, reporterUserId, "CommunityComment", ct);

            case CommunityReportTargetType.Event:
                var ev = await _db.CommunityEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == targetId, ct);
                return ev is not null && await CanViewEventAsync(ev, reporterUserId, ct)
                    ? ev.GroupId
                    : Error.NotFound("CommunityEvent");

            case CommunityReportTargetType.Message:
                var thread = await _db.ThreadMessages
                    .Where(m => m.Id == targetId)
                    .Join(_db.MessageThreads, m => m.ThreadId, t => t.Id, (m, t) => t)
                    .FirstOrDefaultAsync(ct);
                if (thread is null || await RequireThreadAccessAsync(thread.ContextType, thread.ContextId, reporterUserId, ct) is not null)
                {
                    return Error.NotFound("ThreadMessage");
                }

                return await ThreadGroupIdAsync(thread, ct);

            default:
                return Error.Validation("Unknown report target type.");
        }
    }

    private async Task<Result<Guid?>> VisibleGroupOrNotFoundAsync(Guid? groupId, Guid userId, string resource, CancellationToken ct) =>
        groupId is { } gid && await RequireThreadAccessAsync(ThreadContextType.Group, gid, userId, ct) is null
            ? groupId
            : Error.NotFound(resource);

    private async Task<Guid?> ThreadGroupIdAsync(MessageThread thread, CancellationToken ct) =>
        thread.ContextType == ThreadContextType.Group
            ? thread.ContextId
            : await _db.CommunityEvents.Where(e => e.Id == thread.ContextId).Select(e => e.GroupId).FirstOrDefaultAsync(ct);

    /// <summary>
    /// P5-09: one report per (reporter, target) — a repeat is answered with the
    /// existing report. A report hides the target only for the reporter; once
    /// <see cref="DistinctReportersToHoldForEveryone"/> different people have
    /// reported it, it is held for human review for everyone.
    /// </summary>
    /// <summary>
    /// B-2 (Option A, approved 2026-09-30): an authorized safeguarding officer
    /// may MANUALLY convert an escalated unsafe-behavior report into a
    /// safeguarding case. No automatic creation, no user notification. The
    /// report keeps its identity and gets a traceability link back (the case id
    /// recorded on the report row); duplicate conversion is refused. Officer
    /// authorization is enforced server-side from token claims (BR-SG-02).
    /// </summary>
    public async Task<Result<Guid>> ConvertReportToCaseAsync(
        Guid reportId,
        Guid officerUserId,
        bool callerIsSafeguardingOfficer,
        ConvertReportToCaseRequest request,
        CancellationToken ct = default)
    {
        if (!callerIsSafeguardingOfficer)
        {
            return Error.CapabilityMissing("SafeguardingOfficer");
        }

        if (_safeguardingCaseRaiser is null)
        {
            return Error.NotFound("SafeguardingService");
        }

        var report = await _db.CommunityReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
        {
            return Error.NotFound("CommunityReport");
        }

        if (report.Category != CommunityReportCategory.UnsafeBehavior
            || report.Status != CommunityReportStatus.EscalatedToSafeguarding)
        {
            return Error.Validation("Only escalated unsafe-behavior reports can be converted.");
        }

        if (report.SafeguardingCaseId is not null)
        {
            return new Error("REPORT_ALREADY_CONVERTED", "This report was already converted into a safeguarding case.", ErrorKind.Conflict);
        }

        // B-2 (Option A): the conversion crosses the safeguarding boundary via
        // the ISafeguardingCaseRaiser port (Contracts) — no TrustSafety Domain
        // or Application type is referenced here (ModuleBoundaryTests).
        // RaiseCaseAsync fails closed on unknown severity strings; the case's
        // access audit lives inside safeguarding (BR-SG-06).
        var raised = await _safeguardingCaseRaiser.RaiseCaseAsync(
            reporterUserId: officerUserId,
            subjectUserId: report.TargetType == CommunityReportTargetType.Member
                ? report.TargetId
                : throw new InvalidOperationException(
                    "Only member-targeted reports carry a user subject; item reports must be converted from the member context."),
            summary: request.Summary,
            severity: request.Severity,
            category: "community_report",
            organizationId: request.OrganizationId,
            ct: ct);
        if (raised.IsFailure)
        {
            return raised.Error!;
        }

        report.LinkSafeguardingCase(raised.Value!, officerUserId);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(raised.Value!);
    }

    private async Task<Result<CommunityReportDto>> FileReportAsync(
        Guid reporterUserId,
        CommunityReportTargetType targetType,
        Guid targetId,
        CommunityReportCategory category,
        string description,
        Guid? groupId,
        CancellationToken ct)
    {
        var existing = await FindReportAsync(reporterUserId, targetType, targetId, ct);
        if (existing is not null)
        {
            return Result<CommunityReportDto>.Success(MapReport(existing));
        }

        var reportResult = CommunityReport.Create(
            reporterUserId: reporterUserId,
            targetType: targetType,
            targetId: targetId,
            category: category,
            description: description,
            groupId: groupId);

        if (reportResult.IsFailure)
        {
            return reportResult.Error!;
        }

        var report = reportResult.Value!;

        // If the report category is safeguarding-sensitive, escalate immediately.
        if (category == CommunityReportCategory.UnsafeBehavior)
        {
            report.EscalateToSafeguarding(reporterUserId, "Auto-escalated based on report category.");
        }

        _db.CommunityReports.Add(report);

        var otherActiveReporters = await _db.CommunityReports.CountAsync(
            r => r.TargetType == targetType && r.TargetId == targetId
                && (r.Status == CommunityReportStatus.Open
                    || r.Status == CommunityReportStatus.UnderReview
                    || r.Status == CommunityReportStatus.EscalatedToSafeguarding),
            ct);

        if (otherActiveReporters + 1 >= DistinctReportersToHoldForEveryone)
        {
            await HoldReportedContentAsync(targetType, targetId, ct);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var winner = await FindReportAsync(reporterUserId, targetType, targetId, ct);
            return winner is not null ? Result<CommunityReportDto>.Success(MapReport(winner)) : ConcurrencyConflict();
        }

        return Result<CommunityReportDto>.Success(MapReport(report));
    }

    private Task<CommunityReport?> FindReportAsync(Guid reporterUserId, CommunityReportTargetType targetType, Guid targetId, CancellationToken ct) =>
        _db.CommunityReports.AsNoTracking().FirstOrDefaultAsync(
            r => r.ReporterUserId == reporterUserId && r.TargetType == targetType && r.TargetId == targetId, ct);

    private async Task HoldReportedContentAsync(CommunityReportTargetType targetType, Guid targetId, CancellationToken ct)
    {
        const string reason = "Reported by several people — held for review.";
        if (targetType == CommunityReportTargetType.Post)
        {
            var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.Id == targetId, ct);
            if (post?.Status == CommunityPostStatus.Visible)
            {
                post.SetPendingReview(reason);
            }
        }
        else if (targetType == CommunityReportTargetType.Message)
        {
            var message = await _db.ThreadMessages.FirstOrDefaultAsync(m => m.Id == targetId, ct);
            if (message is { IsFlaggedForModeration: false })
            {
                message.FlagForModeration(reason);
            }
        }
    }

    private async Task<HashSet<Guid>> ReportedTargetIdsAsync(
        Guid? reporterUserId, CommunityReportTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct)
    {
        if (reporterUserId is not { } uid)
        {
            return [];
        }

        var ids = targetIds.ToList();
        var reported = await _db.CommunityReports
            .Where(r => r.ReporterUserId == uid && r.TargetType == targetType && ids.Contains(r.TargetId))
            .Select(r => r.TargetId)
            .ToListAsync(ct);
        return reported.ToHashSet();
    }

    // --- Contextual Threads (legacy) ---

    public async Task<Result<MessageThreadDto>> GetOrCreateThreadAsync(
        ThreadContextType contextType,
        Guid contextId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var accessError = await RequireThreadAccessAsync(contextType, contextId, userId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

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
        var thread = await _db.MessageThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Error.NotFound("MessageThread");
        }

        var accessError = await RequireThreadAccessAsync(thread.ContextType, thread.ContextId, requestingUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var messages = await _db.ThreadMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var reportedByMe = await ReportedTargetIdsAsync(requestingUserId, CommunityReportTargetType.Message, messages.Select(m => m.Id), cancellationToken);

        // Held messages stay visible to their sender; messages I reported are hidden for me.
        var visible = messages.Where(m => m.SenderUserId == requestingUserId
            || (!m.IsFlaggedForModeration && !reportedByMe.Contains(m.Id)));

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
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
        {
            return Error.NotFound("MessageThread");
        }

        var accessError = await RequireThreadAccessAsync(thread.ContextType, thread.ContextId, senderUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        if (thread.IsClosed)
        {
            return Error.Conflict("Thread is closed for new messages.");
        }

        if (thread.ContextType == ThreadContextType.Group
            && await _db.GroupMemberships.AnyAsync(
                m => m.GroupId == thread.ContextId && m.UserId == senderUserId && m.Status == GroupMembershipStatus.Muted,
                cancellationToken))
        {
            return new Error("MEMBERSHIP_MUTED", "You are muted in this group and cannot post right now.", ErrorKind.Forbidden);
        }

        var messageResult = ThreadMessage.Create(threadId, senderUserId, request.Content);
        if (messageResult.IsFailure)
        {
            return messageResult.Error!;
        }

        var message = messageResult.Value!;

        var holdReason = await ScreenAsync("message", senderUserId, null, cancellationToken, request.Content);
        if (holdReason is not null)
        {
            message.FlagForModeration(holdReason);
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

        var thread = await _db.MessageThreads.FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);
        var accessError = thread is null
            ? Error.NotFound("MessageThread")
            : await RequireThreadAccessAsync(thread.ContextType, thread.ContextId, userId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
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

        var thread = await _db.MessageThreads.FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);
        var accessError = thread is null
            ? Error.NotFound("MessageThread")
            : await RequireThreadAccessAsync(thread.ContextType, thread.ContextId, reporterUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        // P5-09: a single report no longer silences a message for everyone.
        var reportResult = await FileReportAsync(
            reporterUserId,
            CommunityReportTargetType.Message,
            messageId,
            CommunityReportCategory.Other,
            request.Reason,
            await ThreadGroupIdAsync(thread!, cancellationToken),
            cancellationToken);

        return reportResult.IsFailure
            ? reportResult.Error!
            : Result<ThreadMessageDto>.Success(MapMessage(message));
    }

    // --- Mapping Helpers ---

    private static CommunityGroupDto MapGroup(CommunityGroup g, int memberCount, GroupMembershipStatus? myStatus = null, GroupMemberRole? myRole = null) => new(
        Id: g.Id,
        CreatorUserId: g.CreatorUserId,
        CreatorType: g.CreatorType,
        OrganizationId: g.OrganizationId,
        Name: g.Name,
        Description: g.Description,
        Type: g.Type,
        Category: g.Category,
        Visibility: g.Visibility,
        Status: g.Status,
        City: g.City,
        Region: g.Region,
        PostalCode: g.PostalCode,
        // A meeting place can be someone's living room and groups carry no
        // "public venue" marker, so the central policy treats every group
        // location as PERSONAL: approximate (~1 km grid cell) at most.
        Latitude: LocationVisibilityPolicy.Shape(LocationDisclosure.Approximate, g.Latitude, g.Longitude).Latitude,
        Longitude: LocationVisibilityPolicy.Shape(LocationDisclosure.Approximate, g.Latitude, g.Longitude).Longitude,
        PrimaryLanguageCode: g.PrimaryLanguageCode,
        CoverImageUrl: g.CoverImageUrl,
        Rules: g.Rules,
        MaxMembers: g.MaxMembers,
        MemberCount: memberCount,
        VerificationStatus: g.VerificationStatus,
        CreatedAtUtc: g.CreatedAtUtc,
        MyMembershipStatus: myStatus,
        MyRole: myRole);

    private static GroupMembershipDto MapMembership(GroupMembership m) => new(
        Id: m.Id,
        GroupId: m.GroupId,
        UserId: m.UserId,
        Role: m.Role,
        Status: m.Status,
        JoinedAtUtc: m.JoinedAtUtc,
        MutedUntilUtc: m.MutedUntilUtc,
        BanReason: m.BanReason);

    private static CommunityInvitationDto MapInvitation(CommunityInvitation i) => new(
        Id: i.Id,
        GroupId: i.GroupId,
        InvitedByUserId: i.InvitedByUserId,
        InvitedUserId: i.InvitedUserId,
        Email: i.Email,
        Phone: i.Phone,
        Token: i.Token,
        Status: i.Status,
        ExpiresAtUtc: i.ExpiresAtUtc);

    private static CommunityPostDto MapPost(CommunityPost p) => new(
        Id: p.Id,
        AuthorUserId: p.AuthorUserId,
        GroupId: p.GroupId,
        PostType: p.PostType,
        Title: p.Title,
        Content: p.Content,
        IsPinned: p.IsPinned,
        Status: p.Status,
        ModerationReason: p.ModerationReason,
        CreatedAtUtc: p.CreatedAtUtc,
        UpdatedAtUtc: p.UpdatedAtUtc);

    private static CommunityReportDto MapReport(CommunityReport r) => new(
        Id: r.Id,
        ReporterUserId: r.ReporterUserId,
        TargetType: r.TargetType,
        TargetId: r.TargetId,
        GroupId: r.GroupId,
        Category: r.Category,
        Description: r.Description,
        Status: r.Status,
        CreatedAtUtc: r.CreatedAtUtc);

    private static CommunityEventDto MapEvent(
        CommunityEvent e,
        int goingCount,
        int waitlistCount,
        IReadOnlyCollection<DateTimeOffset>? cancelledOccurrences = null,
        EventRsvpStatus? myRegistrationStatus = null,
        bool isManager = false)
    {
        var cancelledSet = cancelledOccurrences is null
            ? []
            : new HashSet<DateTimeOffset>(cancelledOccurrences);

        var occurrences = e.ComputeOccurrenceStartsUtc()
            .Select(start => new EventOccurrenceDto(start, cancelledSet.Contains(start)))
            .ToList();

        return new(
            Id: e.Id,
            HostUserId: e.HostUserId,
            GroupId: e.GroupId,
            OrganizationId: e.OrganizationId,
            Title: e.Title,
            Description: e.Description,
            Category: e.Category,
            LocationAddress: e.RevealsAddressTo(isManager, myRegistrationStatus) ? e.LocationAddress : null,
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
            CreatedAtUtc: e.CreatedAtUtc,
            Occurrences: occurrences,
            MyRegistrationStatus: myRegistrationStatus,
            IsPendingReview: e.IsPendingReview,
            VenueType: e.VenueType);
    }

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
