using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Identity.Tests;

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
