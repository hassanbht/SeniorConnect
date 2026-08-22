using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class SeniorProfile : Entity, IAuditable
{
    private SeniorProfile() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? AddressLine { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? PostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? City { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Country { get; private set; } = "AT";

    [DataClass(DataClass.Operational)]
    public double? Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Longitude { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? MobilityNote { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? LivingSituation { get; private set; }

    [DataClass(DataClass.Operational)]
    public ContactMethod PreferredContactMethod { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool VulnerabilityFlag { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? VulnerabilitySetByUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? VulnerabilityReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static SeniorProfile Create(
        Guid userId,
        string? addressLine = null,
        string? postalCode = null,
        string? city = null,
        double? latitude = null,
        double? longitude = null,
        string? mobilityNote = null,
        string? livingSituation = null,
        ContactMethod preferredContactMethod = ContactMethod.App,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new SeniorProfile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AddressLine = addressLine,
            PostalCode = postalCode,
            City = city,
            Country = "AT",
            Latitude = latitude,
            Longitude = longitude,
            MobilityNote = mobilityNote,
            LivingSituation = livingSituation,
            PreferredContactMethod = preferredContactMethod,
            VulnerabilityFlag = false,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void SetVulnerability(Guid staffUserId, string reason)
    {
        VulnerabilityFlag = true;
        VulnerabilitySetByUserId = staffUserId;
        VulnerabilityReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = staffUserId;
    }

    public void ClearVulnerability(Guid staffUserId)
    {
        VulnerabilityFlag = false;
        VulnerabilitySetByUserId = null;
        VulnerabilityReason = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = staffUserId;
    }
}

public enum ContactMethod
{
    App,
    Phone,
    Sms,
    Family
}
