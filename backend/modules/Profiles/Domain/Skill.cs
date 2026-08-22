using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class Skill : Entity
{
    private Skill() { }

    [DataClass(DataClass.Operational)]
    public string Code { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string NameKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public bool RequiresVerification { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; } = true;

    public static Skill Create(string code, string nameKey, bool requiresVerification = false)
    {
        return new Skill
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            NameKey = nameKey,
            RequiresVerification = requiresVerification,
            IsActive = true
        };
    }
}
