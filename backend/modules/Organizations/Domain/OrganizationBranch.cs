using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Domain;

public sealed class OrganizationBranch : Entity, IOrganizationScoped, IAuditable
{
    private OrganizationBranch() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Name { get; private set; } = null!;

    [DataClass(DataClass.PersonalData)]
    public string? Address { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? PostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? City { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Longitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static OrganizationBranch Create(
        Guid organizationId,
        string name,
        string? address = null,
        string? postalCode = null,
        string? city = null,
        double? latitude = null,
        double? longitude = null,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new OrganizationBranch
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            Name = name,
            Address = address,
            PostalCode = postalCode,
            City = city,
            Latitude = latitude,
            Longitude = longitude,
            IsActive = true,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }
}
