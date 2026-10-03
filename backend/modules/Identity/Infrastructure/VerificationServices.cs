using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Organizations.Contracts;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Phase 4: the one place verification facts are derived. Read fresh on every
/// call (no snapshot, no cache), so a revoked, expired or lapsed fact stops
/// counting on the next request.
/// </summary>
public sealed class VerificationFactsReader : IVerificationFactsReader
{
    private readonly IIdentityDbContext _db;
    private readonly IOrganizationDirectory? _directory;

    /// <param name="directory">
    /// Without it, organization facts never count (fail closed): an
    /// organization verification lapses when the membership ends.
    /// </param>
    public VerificationFactsReader(IIdentityDbContext db, IOrganizationDirectory? directory = null)
    {
        _db = db;
        _directory = directory;
    }

    public async Task<VerificationFacts> GetAsync(Guid userId, CancellationToken ct = default) =>
        (await GetManyAsync([userId], ct))[userId];

    public async Task<IReadOnlyDictionary<Guid, VerificationFacts>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, VerificationFacts>();
        }

        var now = DateTimeOffset.UtcNow;
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Status, u.EmailVerifiedAtUtc, u.PhoneVerifiedAtUtc })
            .ToListAsync(ct);

        var facts = await _db.Verifications
            .AsNoTracking()
            .Where(v => ids.Contains(v.UserId)
                        && v.Status == VerificationStatus.Verified
                        && (v.ValidUntilUtc == null || v.ValidUntilUtc > now))
            .Select(v => new { v.UserId, v.Type, v.VerifiedByOrganizationId })
            .ToListAsync(ct);

        var memberOrgs = await ActiveMembershipsAsync(
            facts.Where(f => f.Type == VerificationType.Organization && f.VerifiedByOrganizationId != null)
                 .Select(f => f.VerifiedByOrganizationId!.Value),
            ct);

        var result = ids.ToDictionary(id => id, VerificationFacts.Unknown);
        foreach (var u in users)
        {
            var mine = facts.Where(f => f.UserId == u.Id).ToList();
            bool Has(VerificationType t) => mine.Any(f => f.Type == t);
            var orgs = mine
                .Where(f => f.Type == VerificationType.Organization && f.VerifiedByOrganizationId is { } org
                            && memberOrgs.TryGetValue(org, out var roster) && roster.Contains(u.Id))
                .Select(f => f.VerifiedByOrganizationId!.Value)
                .Distinct()
                .ToList();

            result[u.Id] = new VerificationFacts(
                UserId: u.Id,
                IsActiveAccount: u.Status == UserStatus.Active,
                EmailVerified: u.EmailVerifiedAtUtc.HasValue || Has(VerificationType.Email),
                PhoneVerified: u.PhoneVerifiedAtUtc.HasValue || Has(VerificationType.Phone),
                AddressVerified: Has(VerificationType.Address),
                VerifiedByOrganizationIds: orgs);
        }

        return result;
    }

    private async Task<Dictionary<Guid, HashSet<Guid>>> ActiveMembershipsAsync(IEnumerable<Guid> orgIds, CancellationToken ct)
    {
        var rosters = new Dictionary<Guid, HashSet<Guid>>();
        if (_directory is null)
        {
            return rosters;
        }
        foreach (var org in orgIds.Distinct())
        {
            rosters[org] = [.. await _directory.GetActiveVolunteerUserIdsAsync(org, ct)];
        }
        return rosters;
    }
}

public sealed class AddressVerificationInvalidator : IAddressVerificationInvalidator
{
    private const string AddressChangedReason = "The person changed their home address.";
    private readonly IIdentityDbContext _db;

    public AddressVerificationInvalidator(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task InvalidateAddressAsync(Guid userId, CancellationToken ct = default)
    {
        var current = await _db.Verifications
            .Where(v => v.UserId == userId && v.Type == VerificationType.Address && v.Status == VerificationStatus.Verified)
            .ToListAsync(ct);
        if (current.Count == 0)
        {
            return;
        }
        foreach (var v in current)
        {
            v.Revoke(userId, AddressChangedReason);
        }
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class VerificationService : IVerificationService
{
    private readonly IIdentityDbContext _db;
    private readonly IVerificationFactsReader _facts;
    private readonly IOrganizationAccessGuard _guard;
    private readonly IOrganizationDirectory _directory;

    public VerificationService(
        IIdentityDbContext db,
        IVerificationFactsReader facts,
        IOrganizationAccessGuard guard,
        IOrganizationDirectory directory)
    {
        _db = db;
        _facts = facts;
        _guard = guard;
        _directory = directory;
    }

    public async Task<Result<MyVerificationDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.EmailVerifiedAtUtc, u.PhoneVerifiedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (user is null)
        {
            return Error.NotFound("User");
        }

        var facts = await _facts.GetAsync(userId, ct);
        var rows = await _db.Verifications.AsNoTracking()
            .Where(v => v.UserId == userId && v.Status == VerificationStatus.Verified)
            .ToListAsync(ct);
        DateTimeOffset? Latest(VerificationType t) =>
            rows.Where(v => v.Type == t && v.IsCurrentlyValid).Max(v => v.VerifiedAtUtc);

        var badges = new List<VerificationBadgeDto>();
        if (facts.EmailVerified)
        {
            badges.Add(new("EMAIL_VERIFIED", user.EmailVerifiedAtUtc ?? Latest(VerificationType.Email), null, null, null, null));
        }
        if (facts.PhoneVerified)
        {
            badges.Add(new("PHONE_VERIFIED", user.PhoneVerifiedAtUtc ?? Latest(VerificationType.Phone), null, null, null, null));
        }
        if (facts.AddressVerified)
        {
            var row = rows.Where(v => v.Type == VerificationType.Address && v.IsCurrentlyValid)
                .OrderByDescending(v => v.VerifiedAtUtc).First();
            badges.Add(await BadgeAsync("ADDRESS_VERIFIED", row, ct));
        }
        foreach (var row in rows.Where(v => v.Type == VerificationType.Organization && v.IsCurrentlyValid
                                            && v.VerifiedByOrganizationId is { } org
                                            && facts.VerifiedByOrganizationIds.Contains(org)))
        {
            badges.Add(await BadgeAsync("ORGANIZATION_VERIFIED", row, ct));
        }

        var nextStep = facts.Level switch
        {
            VerificationLevel.None => "VERIFY_EMAIL",
            VerificationLevel.Email => "VERIFY_PHONE",
            VerificationLevel.EmailPhone => "VERIFY_ADDRESS",
            _ => null
        };
        return Result<MyVerificationDto>.Success(new MyVerificationDto(LevelCode(facts.Level), badges, nextStep));
    }

    public async Task<Result<StaffVerificationDto>> RecordAsync(
        Guid actorUserId, Guid organizationId, Guid volunteerUserId,
        RecordVerificationRequest request, CancellationToken ct = default)
    {
        var access = await _guard.EnsureStaffAsync(actorUserId, organizationId, ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }
        var type = ParseType(request.Type);
        if (type is null)
        {
            return new Error(VerificationCodes.TypeInvalid, "type must be ADDRESS or ORGANIZATION.", ErrorKind.Validation);
        }
        var method = ParseMethod(request.Method);
        if (method is null)
        {
            return new Error(VerificationCodes.MethodInvalid,
                "method must be DOCUMENT_SIGHTED, ORGANIZATION_RECORD or ORGANIZATION_MEMBERSHIP.", ErrorKind.Validation);
        }

        // Only this organization's own active volunteers; anyone else is
        // invisible to its staff (no cross-tenant verification).
        var roster = await _directory.GetActiveVolunteerUserIdsAsync(organizationId, ct);
        if (!roster.Contains(volunteerUserId))
        {
            return new Error(VerificationCodes.NotAMember, "Volunteer was not found in this organization.", ErrorKind.NotFound);
        }

        var existing = await _db.Verifications.FirstOrDefaultAsync(v =>
            v.UserId == volunteerUserId && v.Type == type && v.VerifiedByOrganizationId == organizationId
            && v.Status == VerificationStatus.Verified, ct);
        if (existing is { IsCurrentlyValid: true })
        {
            return Result<StaffVerificationDto>.Success(Map(existing));
        }

        var created = Verification.RecordManual(
            volunteerUserId, type.Value, method.Value, actorUserId, organizationId, request.ValidUntilUtc);
        if (created.IsFailure)
        {
            return created.Error!;
        }
        _db.Verifications.Add(created.Value!);
        await _db.SaveChangesAsync(ct);
        return Result<StaffVerificationDto>.Success(Map(created.Value!));
    }

    public async Task<Result<StaffVerificationDto>> RevokeAsync(
        Guid actorUserId, Guid organizationId, Guid verificationId,
        RevokeVerificationRequest request, CancellationToken ct = default)
    {
        var access = await _guard.EnsureStaffAsync(actorUserId, organizationId, ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }
        // Staff revoke only what their own organization recorded.
        var verification = await _db.Verifications.FirstOrDefaultAsync(v =>
            v.Id == verificationId && v.VerifiedByOrganizationId == organizationId, ct);
        if (verification is null)
        {
            return Error.NotFound("Verification");
        }
        var revoked = verification.Revoke(actorUserId, request.Reason);
        if (revoked.IsFailure)
        {
            return revoked.Error!;
        }
        await _db.SaveChangesAsync(ct);
        return Result<StaffVerificationDto>.Success(Map(verification));
    }

    private async Task<VerificationBadgeDto> BadgeAsync(string fact, Verification v, CancellationToken ct) => new(
        fact,
        v.VerifiedAtUtc,
        v.ValidUntilUtc,
        v.VerifiedByOrganizationId,
        v.VerifiedByOrganizationId is { } org ? await _directory.GetOrganizationNameAsync(org, ct) : null,
        v.Method is { } m ? MethodCode(m) : null);

    public static string LevelCode(VerificationLevel level) => level switch
    {
        VerificationLevel.Email => "EMAIL",
        VerificationLevel.EmailPhone => "EMAIL_PHONE",
        VerificationLevel.EmailPhoneAddress => "EMAIL_PHONE_ADDRESS",
        _ => "NONE"
    };

    private static VerificationType? ParseType(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "ADDRESS" => VerificationType.Address,
        "ORGANIZATION" => VerificationType.Organization,
        _ => null
    };

    private static VerificationMethod? ParseMethod(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "DOCUMENT_SIGHTED" => VerificationMethod.DocumentSighted,
        "ORGANIZATION_RECORD" => VerificationMethod.OrganizationRecord,
        "ORGANIZATION_MEMBERSHIP" => VerificationMethod.OrganizationMembership,
        _ => null
    };

    private static string MethodCode(VerificationMethod m) => m switch
    {
        VerificationMethod.DocumentSighted => "DOCUMENT_SIGHTED",
        VerificationMethod.OrganizationRecord => "ORGANIZATION_RECORD",
        _ => "ORGANIZATION_MEMBERSHIP"
    };

    private static StaffVerificationDto Map(Verification v) => new(
        v.Id, v.UserId, v.Type.ToString().ToUpperInvariant(), v.Status.ToString().ToUpperInvariant(),
        v.Method is { } m ? MethodCode(m) : null,
        v.VerifiedByOrganizationId, v.VerifiedAtUtc, v.ValidUntilUtc, v.RevokedAtUtc);
}
