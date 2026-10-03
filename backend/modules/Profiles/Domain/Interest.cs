using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class Interest : Entity
{
    private Interest() { }

    [DataClass(DataClass.Operational)]
    public string Code { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string NameKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; } = true;

    public static Interest Create(string code, string nameKey)
    {
        return new Interest
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            NameKey = nameKey,
            IsActive = true
        };
    }
}
