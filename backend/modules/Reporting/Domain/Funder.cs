using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Domain;

public sealed class Funder : Entity, IAuditable
{
    private Funder() { }

    [DataClass(DataClass.Operational)]
    public string Name { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public FunderType Type { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ContactEmail { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ContactPhone { get; private set; }

    [DataClass(DataClass.Operational)]
    public FunderStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Funder Create(
        string name,
        FunderType type,
        string? contactEmail = null,
        string? contactPhone = null,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Funder
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Type = type,
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            Status = FunderStatus.Active,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }
}

public enum FunderType
{
    Municipality,
    Foundation,
    PublicBody,
    Corporate
}

public enum FunderStatus
{
    Active,
    Suspended,
    Ended
}
