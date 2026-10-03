using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Contracts;

/// <summary>
/// Cross-module read contract for verified user trust levels.
/// </summary>
public interface ITrustLevelReader
{
    Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

/// <summary>
/// Cross-module read contract for whether a user account is in Active status
/// (not suspended/deactivated/deleted). Used by eligibility gates such as
/// BR-COMM-06 (ADR-025) before granting community group creation.
/// </summary>
public interface IUserAccountStatusReader
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserContact(string DisplayName, string? Phone);

/// <summary>
/// Cross-module read contract for the minimal contact info BR-COMM-04
/// reveals to an assigned counterparty (never a raw DbSet&lt;User&gt;
/// reference from another module).
/// </summary>
public interface IUserContactReader
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// P6-03: Cross-module contract allowing Family provisioning to create a real User account
/// for a senior with default SeniorMode settings.
/// </summary>
public interface ISeniorAccountProvisioner
{
    Task<Result<Guid>> ProvisionSeniorUserAsync(
        string displayName,
        string? phone,
        Guid createdByUserId,
        CancellationToken ct = default);
}

public sealed record UserSessionDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId,
    string DisplayName);

/// <summary>
/// P6-04: Cross-module contract allowing Family module to issue an active JWT session
/// for a senior when redeeming a valid Zugangskarte.
/// </summary>
public interface IUserSessionIssuer
{
    Task<Result<UserSessionDto>> IssueSessionAsync(
        Guid userId,
        string? deviceLabel = null,
        CancellationToken ct = default);
}

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md): the self-verification
/// path of a person. Computed server-side from persisted facts only; there is
/// no client-writable level anywhere. Organization verification is a separate
/// fact, never a step on this path.
/// </summary>
public enum VerificationLevel
{
    None = 0,
    Email = 1,
    EmailPhone = 2,
    EmailPhoneAddress = 3
}

/// <summary>
/// Phase 4: the verified facts about one person, as of now. A fact counts only
/// while verified, unexpired and unrevoked. No fact implies another: an
/// organization verification is not an address, phone, identity or
/// safeguarding check.
/// </summary>
public sealed record VerificationFacts(
    Guid UserId,
    bool IsActiveAccount,
    bool EmailVerified,
    bool PhoneVerified,
    bool AddressVerified,
    IReadOnlyList<Guid> VerifiedByOrganizationIds)
{
    public static VerificationFacts Unknown(Guid userId) => new(userId, false, false, false, false, []);

    public bool OrganizationVerified => VerifiedByOrganizationIds.Count > 0;

    /// <summary>The ordered path email → phone → address; a later step never counts without the earlier ones.</summary>
    public VerificationLevel Level =>
        !EmailVerified ? VerificationLevel.None
        : !PhoneVerified ? VerificationLevel.Email
        : !AddressVerified ? VerificationLevel.EmailPhone
        : VerificationLevel.EmailPhoneAddress;
}

/// <summary>Phase 4: cross-module read of verification facts (discovery eligibility).</summary>
public interface IVerificationFactsReader
{
    Task<VerificationFacts> GetAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, VerificationFacts>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

/// <summary>
/// Phase 4: an address fact describes one address. When the person changes
/// their home, the old fact stops counting.
/// </summary>
public interface IAddressVerificationInvalidator
{
    Task InvalidateAddressAsync(Guid userId, CancellationToken ct = default);
}
