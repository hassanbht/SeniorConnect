using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Domain;

public sealed class ReferralDirectory : Entity
{
    private ReferralDirectory() { }

    [DataClass(DataClass.Operational)]
    public string ReferralGroup { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string RegionCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string Name { get; private set; } = null!;

    [DataClass(DataClass.PersonalData)]
    public string? Phone { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? Website { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Address { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? NoteKey { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; }

    public static ReferralDirectory Create(
        string referralGroup,
        string regionCode,
        string name,
        string? phone = null,
        string? website = null,
        string? address = null,
        string? noteKey = null)
    {
        return new ReferralDirectory
        {
            Id = Guid.CreateVersion7(),
            ReferralGroup = referralGroup,
            RegionCode = regionCode,
            Name = name,
            Phone = phone,
            Website = website,
            Address = address,
            NoteKey = noteKey,
            IsActive = true
        };
    }
}
