using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Domain;

public sealed class OrganizationMembership : Entity, IOrganizationScoped, IAuditable
{
    private OrganizationMembership() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? BranchId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public MembershipRole Role { get; private set; }

    [DataClass(DataClass.Operational)]
    public MembershipStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? JoinedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? LeftAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static OrganizationMembership Create(
        Guid organizationId,
        Guid userId,
        MembershipRole role,
        Guid? branchId = null,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new OrganizationMembership
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            BranchId = branchId,
            UserId = userId,
            Role = role,
            Status = MembershipStatus.Invited,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void Activate()
    {
        Status = MembershipStatus.Active;
        JoinedAtUtc ??= DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        Status = MembershipStatus.Suspended;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Leave()
    {
        Status = MembershipStatus.Left;
        LeftAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangeRole(MembershipRole newRole)
    {
        Role = newRole;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum MembershipRole
{
    Staff,
    Coordinator,
    Admin,
    SafeguardingOfficer,
    Volunteer,
    Client
}

public enum MembershipStatus
{
    Invited,
    Active,
    Suspended,
    Left
}
