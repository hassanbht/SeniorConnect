using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Identity.Tests;

/// <summary>
/// Wave-3 / ADR-030: effective trust is computed from live verification facts
/// at every trust-consuming boundary. Nothing is authoritative but the facts:
/// a revoked or expired verification stops authorizing on the next read
/// (no re-login, no cache TTL), a deactivated account resolves to zero, and
/// the login-time <c>TrustLevelSnapshot</c> audit row is never read back as
/// authority.
/// </summary>
public sealed class TrustLevelReaderRecalculationTests
{
    private sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
        : DbContext(options), IIdentityDbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<UserCapability> UserCapabilities => Set<UserCapability>();
        public DbSet<Verification> Verifications => Set<Verification>();
        public DbSet<TrustLevelSnapshot> TrustLevelSnapshots => Set<TrustLevelSnapshot>();
        public DbSet<Consent> Consents => Set<Consent>();
        public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
        public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
        public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    }

    private static TestIdentityDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<TestIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddUserAsync(
        TestIdentityDbContext db, params VerificationType[] verifiedTypes)
    {
        var user = User.CreateWithPhone($"+43660{Random.Shared.Next(1000000, 9999999)}", "Volunteer");
        db.Users.Add(user);

        foreach (var type in verifiedTypes)
        {
            var verification = Verification.Create(user.Id, type);
            verification.Verify(Guid.NewGuid());
            db.Verifications.Add(verification);
        }

        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task EffectiveTrust_ReflectsLiveFacts_OnEveryRead()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, VerificationType.Phone, VerificationType.Email);
        var reader = new TrustLevelReader(db);

        Assert.Equal(2, await reader.GetEffectiveTrustLevelAsync(user.Id));
    }

    [Fact]
    public async Task RevokedVerification_DropsEffectiveTrustImmediately()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, VerificationType.Phone, VerificationType.Email);
        var reader = new TrustLevelReader(db);
        Assert.Equal(2, await reader.GetEffectiveTrustLevelAsync(user.Id));

        var phone = await db.Verifications
            .FirstAsync(v => v.UserId == user.Id && v.Type == VerificationType.Phone);
        phone.Revoke(Guid.NewGuid(), "number reassigned");
        await db.SaveChangesAsync();

        Assert.Equal(1, await reader.GetEffectiveTrustLevelAsync(user.Id));
    }

    [Fact]
    public async Task ExpiredVerification_DropsEffectiveTrust()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, VerificationType.Phone, VerificationType.Email);
        var reader = new TrustLevelReader(db);
        Assert.Equal(2, await reader.GetEffectiveTrustLevelAsync(user.Id));

        var email = await db.Verifications
            .FirstAsync(v => v.UserId == user.Id && v.Type == VerificationType.Email);
        email.Expire();
        await db.SaveChangesAsync();

        Assert.Equal(1, await reader.GetEffectiveTrustLevelAsync(user.Id));
    }

    [Fact]
    public async Task DeactivatedAccount_ResolvesToZero()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(
            db,
            VerificationType.Phone,
            VerificationType.Email,
            VerificationType.Address,
            VerificationType.Identity);
        var reader = new TrustLevelReader(db);
        Assert.Equal(4, await reader.GetEffectiveTrustLevelAsync(user.Id));

        var reloaded = await db.Users.FirstAsync(u => u.Id == user.Id);
        reloaded.Deactivate();
        await db.SaveChangesAsync();

        Assert.Equal(0, await reader.GetEffectiveTrustLevelAsync(user.Id));
    }

    [Fact]
    public async Task UnknownUser_ResolvesToZero()
    {
        using var db = CreateDb();
        var reader = new TrustLevelReader(db);

        Assert.Equal(0, await reader.GetEffectiveTrustLevelAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task StaleSnapshot_IsNeverReadAsAuthority()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, VerificationType.Phone);
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(user.Id, 5, "{\"level\":5}"));
        await db.SaveChangesAsync();
        var reader = new TrustLevelReader(db);

        Assert.Equal(1, await reader.GetEffectiveTrustLevelAsync(user.Id));
    }

    [Fact]
    public async Task BatchRead_ResolvesEachAccountFromLiveFacts()
    {
        using var db = CreateDb();
        var active = await AddUserAsync(db, VerificationType.Phone, VerificationType.Email);
        var deactivated = await AddUserAsync(db, VerificationType.Phone, VerificationType.Email);
        var reloaded = await db.Users.FirstAsync(u => u.Id == deactivated.Id);
        reloaded.Deactivate();
        await db.SaveChangesAsync();
        var reader = new TrustLevelReader(db);

        var levels = await reader.GetEffectiveTrustLevelsAsync([active.Id, deactivated.Id, Guid.NewGuid()]);

        Assert.Equal(2, levels[active.Id]);
        Assert.Equal(0, levels[deactivated.Id]);
        Assert.DoesNotContain(Guid.NewGuid(), levels.Keys);
    }
}
