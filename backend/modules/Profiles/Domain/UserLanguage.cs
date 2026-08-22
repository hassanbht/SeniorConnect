using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class UserLanguage
{
    private UserLanguage() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid LanguageId { get; private set; }

    [DataClass(DataClass.Operational)]
    public LanguageProficiency Proficiency { get; private set; } = LanguageProficiency.B1;

    public static UserLanguage Create(Guid userId, Guid languageId, LanguageProficiency proficiency = LanguageProficiency.B1)
    {
        return new UserLanguage
        {
            UserId = userId,
            LanguageId = languageId,
            Proficiency = proficiency
        };
    }
}

public enum LanguageProficiency
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
    Native
}
