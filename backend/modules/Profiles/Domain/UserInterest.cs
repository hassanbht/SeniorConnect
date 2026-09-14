using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class UserInterest
{
    private UserInterest() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid InterestId { get; private set; }

    public static UserInterest Create(Guid userId, Guid interestId) => new()
    {
        UserId = userId,
        InterestId = interestId
    };
}
