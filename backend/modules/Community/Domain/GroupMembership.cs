using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum GroupMemberRole
{
    Owner,
    Admin,
    Moderator,
    Member
}

public enum GroupMembershipStatus
{
    Pending,
    Active,
    Rejected,
    Left,
    Removed,
    Muted,
    Banned
}

public sealed class GroupMembership : Entity, IAuditable
{
    private GroupMembership() { }

    [DataClass(DataClass.Operational)]
    public Guid GroupId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public GroupMemberRole Role { get; private set; } = GroupMemberRole.Member;

    [DataClass(DataClass.Operational)]
    public GroupMembershipStatus Status { get; private set; } = GroupMembershipStatus.Active;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset JoinedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RequestedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? MutedUntilUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? BanReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? InvitedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? InvitationToken { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static GroupMembership CreateOwner(Guid groupId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupMemberRole.Owner,
            Status = GroupMembershipStatus.Active,
            JoinedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public static GroupMembership JoinDirectly(Guid groupId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupMemberRole.Member,
            Status = GroupMembershipStatus.Active,
            JoinedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public static GroupMembership RequestToJoin(Guid groupId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupMemberRole.Member,
            Status = GroupMembershipStatus.Pending,
            RequestedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public static GroupMembership CreateInvitation(Guid groupId, Guid invitedUserId, Guid invitedByUserId, string token)
    {
        var now = DateTimeOffset.UtcNow;
        return new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = invitedUserId,
            Role = GroupMemberRole.Member,
            Status = GroupMembershipStatus.Pending,
            InvitedByUserId = invitedByUserId,
            InvitationToken = token,
            CreatedAtUtc = now,
            CreatedBy = invitedByUserId,
            UpdatedAtUtc = now,
            UpdatedBy = invitedByUserId
        };
    }

    public Result Approve(Guid approverUserId)
    {
        if (Status != GroupMembershipStatus.Pending)
        {
            return Error.Conflict("Membership is not pending approval.");
        }

        Status = GroupMembershipStatus.Active;
        JoinedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = approverUserId;
        return Result.Success();
    }

    public Result AcceptInvitation(Guid acceptingUserId)
    {
        if (Status != GroupMembershipStatus.Pending || string.IsNullOrEmpty(InvitationToken))
        {
            return Error.Conflict("Membership is not a pending invitation.");
        }

        Status = GroupMembershipStatus.Active;
        JoinedAtUtc = DateTimeOffset.UtcNow;
        InvitationToken = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = acceptingUserId;
        return Result.Success();
    }

    public Result Reject(Guid rejectorUserId)
    {
        if (Status != GroupMembershipStatus.Pending)
        {
            return Error.Conflict("Membership is not pending approval.");
        }

        Status = GroupMembershipStatus.Rejected;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = rejectorUserId;
        return Result.Success();
    }

    public Result Promote(GroupMemberRole newRole, Guid adminUserId)
    {
        if (Status != GroupMembershipStatus.Active)
        {
            return Error.Conflict("Cannot change role of inactive member.");
        }

        if (newRole == GroupMemberRole.Owner)
        {
            return Error.Forbidden("Ownership transfer uses a dedicated flow.");
        }

        Role = newRole;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = adminUserId;
        return Result.Success();
    }

    public Result Mute(Guid moderatorUserId, DateTimeOffset untilUtc, string? reason = null)
    {
        if (Status != GroupMembershipStatus.Active && Status != GroupMembershipStatus.Muted)
        {
            return Error.Conflict("Can only mute active members.");
        }

        if (untilUtc <= DateTimeOffset.UtcNow)
        {
            return Error.Validation("Mute end time must be in the future.");
        }

        Status = GroupMembershipStatus.Muted;
        MutedUntilUtc = untilUtc;
        BanReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
        return Result.Success();
    }

    public Result Unmute(Guid moderatorUserId)
    {
        if (Status != GroupMembershipStatus.Muted)
        {
            return Error.Conflict("Member is not muted.");
        }

        Status = GroupMembershipStatus.Active;
        MutedUntilUtc = null;
        BanReason = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
        return Result.Success();
    }

    public Result Ban(Guid moderatorUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Ban reason is required.");
        }

        if (Status == GroupMembershipStatus.Banned)
        {
            return Error.Conflict("Member is already banned.");
        }

        Status = GroupMembershipStatus.Banned;
        BanReason = reason.Trim();
        MutedUntilUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
        return Result.Success();
    }

    public Result Unban(Guid moderatorUserId)
    {
        if (Status != GroupMembershipStatus.Banned)
        {
            return Error.Conflict("Member is not banned.");
        }

        Status = GroupMembershipStatus.Active;
        BanReason = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
        return Result.Success();
    }

    /// <summary>
    /// P5-02: re-uses the one membership row per (group, user) instead of adding a
    /// second one. Only a row that no longer grants or restricts anything may be
    /// reopened — Muted/Banned/Active rows must never be overwritten by a join.
    /// Removed is allowed here because the service only calls this for Removed on
    /// an explicit invitation by a group admin.
    /// </summary>
    public Result Rejoin(bool requiresApproval, Guid actorUserId)
    {
        if (Status is not (GroupMembershipStatus.Left
            or GroupMembershipStatus.Rejected
            or GroupMembershipStatus.Removed
            or GroupMembershipStatus.Pending))
        {
            return Error.Conflict("Membership cannot be reopened from its current state.");
        }

        var now = DateTimeOffset.UtcNow;
        Role = GroupMemberRole.Member;
        Status = requiresApproval ? GroupMembershipStatus.Pending : GroupMembershipStatus.Active;
        if (requiresApproval)
        {
            RequestedAtUtc = now;
        }
        else
        {
            JoinedAtUtc = now;
        }

        MutedUntilUtc = null;
        BanReason = null;
        InvitationToken = null;
        UpdatedAtUtc = now;
        UpdatedBy = actorUserId;
        return Result.Success();
    }

    public void Leave()
    {
        Status = GroupMembershipStatus.Left;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = UserId;
    }

    public void Remove(Guid adminUserId)
    {
        Status = GroupMembershipStatus.Removed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = adminUserId;
    }
}
