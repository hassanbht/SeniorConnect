using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Identity.Tests;

/// <summary>
/// Wave-1 negative tests for PerformSafetyLevel* derivation
/// (docs/architecture/authorization.md §3).
///
/// The capability must NOT be a plain trust-level ladder: trust level 4 means
/// "official identity verified", so deriving the home-visit level from the
/// level alone would hand a stranger's home access to anyone whose passport
/// was checked. Each level 3–5 additionally requires its own recorded facts.
/// </summary>
public sealed class CapabilitySafetyLevelTests
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

    private static async Task<(CapabilityService Service, TestIdentityDbContext Db, User User)> CreateAsync(
        params VerificationType[] verifiedTypes)
    {
        var db = new TestIdentityDbContext(new DbContextOptionsBuilder<TestIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var user = User.CreateWithPhone("+436601234567", "Volunteer");
        db.Users.Add(user);

        foreach (var type in verifiedTypes)
        {
            var verification = Verification.Create(user.Id, type);
            verification.Verify(Guid.NewGuid());
            db.Verifications.Add(verification);
        }

        await db.SaveChangesAsync();
        return (new CapabilityService(db), db, user);
    }

    // --- the home-visit gate ------------------------------------------------

    [Fact]
    public async Task TrustLevel4_alone_never_grants_the_home_visit_level()
    {
        // Identity verified only — the exact combination the old ladder
        // mistook for a background check.
        var (service, _, user) = await CreateAsync(VerificationType.Identity);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 4);

        Assert.DoesNotContain("PerformSafetyLevel4", capabilities);
        Assert.DoesNotContain("PerformSafetyLevel5", capabilities);
    }

    [Fact]
    public async Task Background_check_without_organization_approval_never_grants_the_home_visit_level()
    {
        var (service, _, user) = await CreateAsync(VerificationType.BackgroundCheck);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 5);

        Assert.DoesNotContain("PerformSafetyLevel4", capabilities);
    }

    [Fact]
    public async Task TrustLevel5_with_background_check_and_organization_approval_grants_the_home_visit_level()
    {
        var (service, _, user) = await CreateAsync(
            VerificationType.BackgroundCheck, VerificationType.Organization);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 5);

        Assert.Contains("PerformSafetyLevel4", capabilities);
    }

    // --- level 3, the training + approval gate ------------------------------

    [Fact]
    public async Task TrustLevel3_without_training_never_grants_level3()
    {
        var (service, _, user) = await CreateAsync(VerificationType.Organization);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 3);

        Assert.DoesNotContain("PerformSafetyLevel3", capabilities);
    }

    [Fact]
    public async Task TrustLevel3_with_training_and_organization_approval_grants_level3()
    {
        var (service, _, user) = await CreateAsync(
            VerificationType.Training, VerificationType.Organization);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 3);

        Assert.Contains("PerformSafetyLevel3", capabilities);
    }

    // --- level 5 stays an assignment-time decision --------------------------

    [Fact]
    public async Task Level5_is_never_granted_from_a_user_level_fact_alone()
    {
        // It additionally needs a named coordinator for the specific activity,
        // which is an assignment-time decision, not a user-level fact.
        var (service, _, user) = await CreateAsync(
            VerificationType.Training, VerificationType.BackgroundCheck, VerificationType.Organization);

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 5);

        Assert.DoesNotContain("PerformSafetyLevel5", capabilities);
    }

    // --- revocation and hand-granting ---------------------------------------

    [Fact]
    public async Task An_expired_background_check_stops_counting_immediately()
    {
        var (service, db, user) = await CreateAsync(VerificationType.Organization);

        var expired = Verification.Create(user.Id, VerificationType.BackgroundCheck);
        expired.Verify(Guid.NewGuid(), validUntilUtc: DateTimeOffset.UtcNow.AddDays(-1));
        db.Verifications.Add(expired);
        await db.SaveChangesAsync();

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 5);

        Assert.DoesNotContain("PerformSafetyLevel4", capabilities);
    }

    [Fact]
    public async Task A_hand_granted_safety_level_is_still_ignored()
    {
        var (service, db, user) = await CreateAsync();
        db.UserCapabilities.Add(UserCapability.Granted(
            user.Id, "PerformSafetyLevel4", Guid.NewGuid(), "test grant"));
        await db.SaveChangesAsync();

        var capabilities = await service.ResolveCapabilitiesAsync(user, currentTrustLevel: 0);

        Assert.DoesNotContain("PerformSafetyLevel4", capabilities);
    }
}

public sealed class TrustLevelCalculatorTests
{
    private readonly TrustLevelCalculator _calculator = new();

    [Fact]
    public void UnverifiedUser_ReturnsTrustLevel0()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        var verifications = Array.Empty<Verification>();

        var eval = _calculator.Evaluate(user, verifications);

        Assert.Equal(0, eval.Level);
        Assert.Contains("verification.phone_or_email", eval.MissingForNextLevel);
    }

    [Fact]
    public void PhoneVerifiedUser_ReturnsTrustLevel1()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();

        var verifications = Array.Empty<Verification>();
        var eval = _calculator.Evaluate(user, verifications);

        Assert.Equal(1, eval.Level);
        Assert.Contains("verification.email", eval.MissingForNextLevel);
    }

    [Fact]
    public void PhoneAndEmailVerifiedUser_ReturnsTrustLevel2()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.ChangeEmail("test@SeniorConnect.at");
        user.VerifyEmail();

        var verifications = Array.Empty<Verification>();
        var eval = _calculator.Evaluate(user, verifications);

        Assert.Equal(2, eval.Level);
        Assert.Contains("verification.address", eval.MissingForNextLevel);
    }

    [Fact]
    public void Level2PlusAddressVerified_ReturnsTrustLevel3()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.ChangeEmail("test@SeniorConnect.at");
        user.VerifyEmail();

        var verifications = new List<Verification>
        {
            Verification.Create(user.Id, VerificationType.Address, null)
        };
        verifications[0].RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, verifications);

        Assert.Equal(3, eval.Level);
        Assert.Contains("verification.identity", eval.MissingForNextLevel);
    }

    [Fact]
    public void Level3PlusIdentityVerified_ReturnsTrustLevel4()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.ChangeEmail("test@SeniorConnect.at");
        user.VerifyEmail();

        var v1 = Verification.Create(user.Id, VerificationType.Address, null);
        v1.RecordOutcome(VerificationStatus.Verified);

        var v2 = Verification.Create(user.Id, VerificationType.Identity, null);
        v2.RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, new[] { v1, v2 });

        Assert.Equal(4, eval.Level);
        Assert.Contains("verification.background_check", eval.MissingForNextLevel);
    }

    [Fact]
    public void Level4PlusBackgroundCheck_ReturnsTrustLevel5()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.ChangeEmail("test@SeniorConnect.at");
        user.VerifyEmail();

        var v1 = Verification.Create(user.Id, VerificationType.Address, null);
        v1.RecordOutcome(VerificationStatus.Verified);

        var v2 = Verification.Create(user.Id, VerificationType.Identity, null);
        v2.RecordOutcome(VerificationStatus.Verified);

        var v3 = Verification.Create(user.Id, VerificationType.BackgroundCheck, null);
        v3.RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, new[] { v1, v2, v3 });

        Assert.Equal(5, eval.Level);
        Assert.Empty(eval.MissingForNextLevel);
    }
}

