using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// Core user entity. A user may hold both SupportProfile and VolunteerProfile.
/// Password is ONLY for organization staff (ADR-016 / BR-AUTH-01..03).
/// </summary>
public sealed class User : Entity, IAuditable, ISoftDeletable
{
    private User() { }

    [DataClass(DataClass.PersonalData)]
    public string? Email { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Phone { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? PhoneVerifiedAtUtc { get; private set; }

    /// <summary>
    /// NULLABLE. A senior or volunteer account has NO password (ADR-016).
    /// Only staff with primary_auth_method = 'password' have this set.
    /// </summary>
    [DataClass(DataClass.SensitiveData)]
    public string? PasswordHash { get; private set; }

    [DataClass(DataClass.Operational)]
    public AuthMethod PrimaryAuthMethod { get; private set; }

    [DataClass(DataClass.PublicProfile)]
    public string DisplayName { get; private set; } = null!;

    [DataClass(DataClass.PersonalData)]
    public DateOnly? DateOfBirth { get; private set; }

    [DataClass(DataClass.Operational)]
    public string PreferredLocale { get; private set; } = "de";

    [DataClass(DataClass.Operational)]
    public bool SeniorModeDefault { get; private set; }

    [DataClass(DataClass.Operational)]
    public UserStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDeleted { get; private set; }

    public static User CreateWithPhone(
        string phone,
        string displayName,
        string preferredLocale = "de",
        bool seniorModeDefault = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.CreateVersion7(),
            Phone = phone,
            DisplayName = displayName,
            PrimaryAuthMethod = AuthMethod.PhoneOtp,
            PreferredLocale = preferredLocale,
            SeniorModeDefault = seniorModeDefault,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsDeleted = false
        };
    }

    public static User CreateWithEmail(
        string email,
        string displayName,
        string preferredLocale = "de")
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            DisplayName = displayName,
            PrimaryAuthMethod = AuthMethod.EmailMagicLink,
            PreferredLocale = preferredLocale,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsDeleted = false
        };
    }

    public static User CreateStaff(
        string email,
        string displayName,
        string passwordHash,
        string preferredLocale = "de")
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            PrimaryAuthMethod = AuthMethod.Password,
            PreferredLocale = preferredLocale,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsDeleted = false
        };
    }

    public void VerifyPhone()
    {
        PhoneVerifiedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void VerifyEmail()
    {
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangePhone(string newPhone)
    {
        Phone = newPhone;
        PhoneVerifiedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangeEmail(string newEmail)
    {
        Email = newEmail;
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string displayName, DateOnly? dateOfBirth, string preferredLocale, bool seniorModeDefault)
    {
        DisplayName = displayName;
        DateOfBirth = dateOfBirth;
        PreferredLocale = preferredLocale;
        SeniorModeDefault = seniorModeDefault;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Status = UserStatus.Deactivated;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        Status = UserStatus.Deleted;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AnonymizeForGdpr()
    {
        DisplayName = "Gelöschtes Profil";
        Email = null;
        Phone = null;
        DateOfBirth = null;
        PasswordHash = null;
        IsDeleted = true;
        Status = UserStatus.Deleted;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum AuthMethod
{
    PhoneOtp,
    EmailMagicLink,
    Password
}

public enum UserStatus
{
    Active,
    Suspended,
    Deactivated,
    Deleted
}
