using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class SeniorAccountProvisioner : ISeniorAccountProvisioner, IUserSessionIssuer
{
    private readonly IIdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ICapabilityService _capabilityService;
    private readonly ITrustLevelCalculator _trustCalculator;

    public SeniorAccountProvisioner(
        IIdentityDbContext db,
        ITokenService tokenService,
        ICapabilityService capabilityService,
        ITrustLevelCalculator trustCalculator)
    {
        _db = db;
        _tokenService = tokenService;
        _capabilityService = capabilityService;
        _trustCalculator = trustCalculator;
    }

    public async Task<Result<Guid>> ProvisionSeniorUserAsync(
        string displayName,
        string? phone,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Error.Validation("Senior display name is required.");
        }

        var user = User.CreatePreprovisionedSenior(
            displayName: displayName.Trim(),
            phone: string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            createdBy: createdByUserId,
            preferredLocale: "de");

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return user.Id;
    }

    public async Task<Result<UserSessionDto>> IssueSessionAsync(
        Guid userId,
        string? deviceLabel = null,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

        if (user is null)
        {
            return Error.NotFound("User");
        }

        var verifications = await _db.Verifications
            .Where(v => v.UserId == user.Id)
            .ToListAsync(ct);

        var trustEval = _trustCalculator.Evaluate(user, verifications);
        var capabilities = await _capabilityService.ResolveCapabilitiesAsync(user, trustEval.Level, ct);
        var accessToken = _tokenService.GenerateAccessToken(user, capabilities, trustEval.Level);

        var refreshResult = await _tokenService.CreateRefreshTokenAsync(
            user.Id,
            deviceLabel ?? "Zugangskarte Device",
            isPersonalDevice: true,
            cancellationToken: ct);

        if (refreshResult.IsFailure)
        {
            return refreshResult.Error!;
        }

        return new UserSessionDto(
            AccessToken: accessToken,
            RefreshToken: refreshResult.Value.RawToken,
            ExpiresInSeconds: 900,
            UserId: user.Id,
            DisplayName: user.DisplayName);
    }
}
