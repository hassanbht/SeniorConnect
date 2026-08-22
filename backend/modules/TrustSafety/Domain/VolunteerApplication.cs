using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

public sealed class VolunteerApplication : Entity, IOrganizationScoped, IAuditable
{
    private VolunteerApplication() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public ApplicationStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Motivation { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset AppliedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? DecidedByUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? DeclineReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static VolunteerApplication Apply(
        Guid organizationId,
        Guid userId,
        string? motivation = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new VolunteerApplication
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            UserId = userId,
            Motivation = motivation,
            Status = ApplicationStatus.Open,
            AppliedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public void Approve(Guid decidedByUserId)
    {
        Status = ApplicationStatus.Approved;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = decidedByUserId;
    }

    public void Decline(Guid decidedByUserId, string? reason)
    {
        Status = ApplicationStatus.Declined;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        DeclineReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = decidedByUserId;
    }

    public void Withdraw()
    {
        Status = ApplicationStatus.Withdrawn;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum ApplicationStatus
{
    Open,
    Approved,
    Declined,
    Withdrawn
}
