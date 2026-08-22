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
}

public enum ApplicationStatus
{
    Open,
    Approved,
    Declined,
    Withdrawn
}
