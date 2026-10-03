using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IIdentityDbContext
{
    DbSet<User> Users { get; }
    DbSet<OtpChallenge> OtpChallenges { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserCapability> UserCapabilities { get; }
    DbSet<Verification> Verifications { get; }
    DbSet<TrustLevelSnapshot> TrustLevelSnapshots { get; }
    DbSet<Consent> Consents { get; }
    DbSet<AccountDeletionRequest> AccountDeletionRequests { get; }
    DbSet<UserExternalLogin> UserExternalLogins { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
