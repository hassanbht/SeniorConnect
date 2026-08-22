using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class PrivacyService : IPrivacyService
{
    private readonly IIdentityDbContext _db;

    public PrivacyService(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<VersionedConsentDto>>> GetUserConsentsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var consents = await _db.Consents
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.GrantedAtUtc)
            .ToListAsync(ct);

        return consents.Select(MapConsentToDto).ToList();
    }

    public async Task<Result<VersionedConsentDto>> RecordConsentAsync(
        Guid userId,
        RecordConsentRequest request,
        string? ipHash = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentVersion))
            return Error.Validation("Document version is required.");

        var consent = Consent.Create(
            userId,
            request.ConsentType,
            request.DocumentVersion.Trim(),
            request.Granted,
            ipHash);

        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(ct);

        return MapConsentToDto(consent);
    }

    public async Task<Result<VersionedConsentDto>> WithdrawConsentAsync(
        Guid userId,
        ConsentType consentType,
        CancellationToken ct = default)
    {
        var activeConsent = await _db.Consents
            .Where(c => c.UserId == userId && c.ConsentType == consentType && c.Granted)
            .OrderByDescending(c => c.GrantedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (activeConsent is null)
        {
            return Error.NotFound("Active consent");
        }

        activeConsent.Withdraw();
        await _db.SaveChangesAsync(ct);

        return MapConsentToDto(activeConsent);
    }

    public async Task<Result<UserDataExportDto>> ExportUserDataAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("User");

        var consents = await _db.Consents
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.ConsentType,
                c.DocumentVersion,
                c.Granted,
                c.GrantedAtUtc,
                c.WithdrawnAtUtc
            })
            .ToListAsync(ct);

        var userProfile = new
        {
            user.Id,
            user.Phone,
            user.Email,
            user.DisplayName,
            user.PrimaryAuthMethod,
            user.CreatedAtUtc,
            Status = user.Status.ToString()
        };

        var export = new UserDataExportDto(
            userId,
            DateTimeOffset.UtcNow,
            "JSON-GDPR-PORTABLE-v1",
            userProfile,
            consents.Cast<object>().ToList(),
            new List<object>());

        return export;
    }

    public async Task<Result<DeletionRequestDto>> RequestAccountDeletionAsync(
        Guid userId,
        RequestDeletionRequest request,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("User");

        var deletionReq = AccountDeletionRequest.Create(userId, request.Reason);
        _db.AccountDeletionRequests.Add(deletionReq);
        await _db.SaveChangesAsync(ct);

        return MapDeletionToDto(deletionReq);
    }

    public async Task<Result<DeletionRequestDto>> ConfirmAccountDeletionAsync(
        Guid userId,
        ConfirmDeletionRequest request,
        CancellationToken ct = default)
    {
        var deletionReq = await _db.AccountDeletionRequests
            .Where(r => r.UserId == userId && r.Status == DeletionTierStatus.Tier1Requested)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (deletionReq is null)
        {
            return Error.NotFound("Active deletion request");
        }

        var confirmResult = deletionReq.ConfirmAndExecuteTier1(request.ConfirmationToken);
        if (confirmResult.IsFailure) return confirmResult.Error!;

        // Tier-1 Execution: Deactivate user and revoke all refresh tokens
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not null)
        {
            user.Deactivate();
        }

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens)
        {
            t.Revoke();
        }

        await _db.SaveChangesAsync(ct);
        return MapDeletionToDto(deletionReq);
    }

    private static VersionedConsentDto MapConsentToDto(Consent c) =>
        new(
            c.Id,
            c.UserId,
            c.ConsentType,
            c.DocumentVersion,
            c.Granted,
            c.GrantedAtUtc,
            c.WithdrawnAtUtc);

    private static DeletionRequestDto MapDeletionToDto(AccountDeletionRequest r) =>
        new(
            r.Id,
            r.UserId,
            r.Status,
            r.RequestedAtUtc,
            r.ScheduledTier2PurgeUtc,
            r.Tier1ExecutedAtUtc);
}
