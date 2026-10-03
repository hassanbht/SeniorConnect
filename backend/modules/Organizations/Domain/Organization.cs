using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Domain;

public sealed class Organization : Entity, IAuditable, ISoftDeletable
{
    private Organization() { }

    [DataClass(DataClass.Operational)]
    public string Name { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string? LegalName { get; private set; }

    [DataClass(DataClass.Operational)]
    public OrganizationType Type { get; private set; }

    [DataClass(DataClass.Operational)]
    public OrganizationStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? SupportEmail { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? SupportPhone { get; private set; }

    [DataClass(DataClass.Operational)]
    public string BrandingJson { get; private set; } = "{}";

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDeleted { get; private set; }

    public static Organization Create(
        string name,
        OrganizationType type,
        string? legalName = null,
        string? supportEmail = null,
        string? supportPhone = null,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Organization
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            LegalName = legalName,
            Type = type,
            Status = OrganizationStatus.Active,
            SupportEmail = supportEmail,
            SupportPhone = supportPhone,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy,
            IsDeleted = false
        };
    }
}

public enum OrganizationType
{
    Ngo,
    Association,
    Parish,
    Company,
    Other
}

public enum OrganizationStatus
{
    Active,
    Suspended,
    Archived
}
