using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

public sealed class UserExternalLogin : Entity
{
    private UserExternalLogin() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Provider { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string ProviderKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string? Email { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DisplayName { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset LinkedAtUtc { get; private set; }

    public static UserExternalLogin Create(
        Guid userId,
        string provider,
        string providerKey,
        string? email = null,
        string? displayName = null)
    {
        return new UserExternalLogin
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            ProviderKey = providerKey,
            Email = email,
            DisplayName = displayName,
            LinkedAtUtc = DateTimeOffset.UtcNow
        };
    }
}