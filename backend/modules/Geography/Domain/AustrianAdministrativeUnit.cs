using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Geography.Domain;

public sealed class AustrianAdministrativeUnit : Entity, IAuditable
{
    private AustrianAdministrativeUnit() { }

    [DataClass(DataClass.Operational)]
    public string BundeslandCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string BundeslandName { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string BezirkCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string BezirkName { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string GemeindeCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string GemeindeName { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string PostalCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string LocalityName { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public double Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double Longitude { get; private set; }

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

    public static AustrianAdministrativeUnit Create(
        string bundeslandCode,
        string bundeslandName,
        string bezirkCode,
        string bezirkName,
        string gemeindeCode,
        string gemeindeName,
        string postalCode,
        string localityName,
        double latitude,
        double longitude,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new AustrianAdministrativeUnit
        {
            Id = Guid.CreateVersion7(),
            BundeslandCode = bundeslandCode,
            BundeslandName = bundeslandName,
            BezirkCode = bezirkCode,
            BezirkName = bezirkName,
            GemeindeCode = gemeindeCode,
            GemeindeName = gemeindeName,
            PostalCode = postalCode,
            LocalityName = localityName,
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



