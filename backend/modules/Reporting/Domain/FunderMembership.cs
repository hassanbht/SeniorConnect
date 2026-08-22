using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Domain;

public sealed class FunderMembership : Entity
{
    private FunderMembership() { }

    [DataClass(DataClass.Operational)]
    public Guid FunderId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public FunderRole Role { get; private set; }

    [DataClass(DataClass.Operational)]
    public FunderMembershipStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? JoinedAtUtc { get; private set; }

    public static FunderMembership Create(
        Guid funderId,
        Guid userId,
        FunderRole role = FunderRole.Viewer)
    {
        return new FunderMembership
        {
            Id = Guid.CreateVersion7(),
            FunderId = funderId,
            UserId = userId,
            Role = role,
            Status = FunderMembershipStatus.Invited
        };
    }
}

public enum FunderRole
{
    Viewer,
    Admin
}

public enum FunderMembershipStatus
{
    Invited,
    Active,
    Suspended,
    Left
}
