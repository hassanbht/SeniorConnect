using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class Language : Entity
{
    private Language() { }

    [DataClass(DataClass.Operational)]
    public string IsoCode { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string NameKey { get; private set; } = null!;

    public static Language Create(string isoCode, string nameKey)
    {
        return new Language
        {
            Id = Guid.CreateVersion7(),
            IsoCode = isoCode,
            NameKey = nameKey
        };
    }
}
