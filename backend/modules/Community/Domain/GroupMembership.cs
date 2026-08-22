using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum GroupMemberRole
{
    Owner,
    Admin,
    Member
}

public enum GroupMembershipStatus
{
    Active,
    PendingApproval,
    Invited,
    Rejected,
    Left,
    Removed
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
            Status = GroupMembershipStatus.PendingApproval,
            JoinedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public Result Approve(Guid approverUserId)
    {
        if (Status != GroupMembershipStatus.PendingApproval)
        {
            return Error.Conflict("Membership is not pending approval.");
        }

        Status = GroupMembershipStatus.Active;
        JoinedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = approverUserId;
        return Result.Success();
    }

    public Result Reject(Guid rejectorUserId)
    {
        if (Status != GroupMembershipStatus.PendingApproval)
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

        Role = newRole;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = adminUserId;
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
