using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Family.Domain;

public sealed class TrustedContact
{
    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string Relationship { get; private set; } = string.Empty;
    public bool IsPrimaryEmergency { get; private set; }
    public bool NotifyOnSafetyAlert { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }

    private TrustedContact() { }

    public static Result<TrustedContact> Create(
        Guid seniorUserId,
        string name,
        string phoneNumber,
        string relationship,
        string? email = null,
        bool isPrimaryEmergency = false,
        bool notifyOnSafetyAlert = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Name is required.");

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Error.Validation("Phone number is required.");

        return new TrustedContact
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            Name = name.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            Email = email?.Trim(),
            Relationship = relationship.Trim(),
            IsPrimaryEmergency = isPrimaryEmergency,
            NotifyOnSafetyAlert = notifyOnSafetyAlert,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
    }

    public void Update(
        string name,
        string phoneNumber,
        string relationship,
        string? email,
        bool isPrimaryEmergency,
        bool notifyOnSafetyAlert)
    {
        Name = name.Trim();
        PhoneNumber = phoneNumber.Trim();
        Email = email?.Trim();
        Relationship = relationship.Trim();
        IsPrimaryEmergency = isPrimaryEmergency;
        NotifyOnSafetyAlert = notifyOnSafetyAlert;
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}
