using System.Text.Json;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class TrustLevelCalculator : ITrustLevelCalculator
{
    public TrustLevelEvaluation Evaluate(User user, IReadOnlyList<Verification> verifications)
    {
        var hasVerifiedPhone = user.PhoneVerifiedAtUtc.HasValue
                               || verifications.Any(v => v.Type == VerificationType.Phone && v.Status == VerificationStatus.Verified && !v.IsExpired);

        var hasVerifiedEmail = user.EmailVerifiedAtUtc.HasValue
                               || verifications.Any(v => v.Type == VerificationType.Email && v.Status == VerificationStatus.Verified && !v.IsExpired);

        var hasVerifiedAddress = verifications.Any(v => v.Type == VerificationType.Address && v.Status == VerificationStatus.Verified && !v.IsExpired);
        var hasVerifiedIdentity = verifications.Any(v => v.Type == VerificationType.Identity && v.Status == VerificationStatus.Verified && !v.IsExpired);
        var hasVerifiedBackground = verifications.Any(v => v.Type == VerificationType.BackgroundCheck && v.Status == VerificationStatus.Verified && !v.IsExpired);

        short computedLevel = 0;
        var reasons = new List<string>();
        var missingForNext = new List<string>();

        if (hasVerifiedPhone || hasVerifiedEmail)
        {
            computedLevel = 1;
            reasons.Add("Phone or Email verified");
        }
        else
        {
            missingForNext.Add("verification.phone_or_email");
        }

        if (computedLevel >= 1)
        {
            if (hasVerifiedPhone && hasVerifiedEmail)
            {
                computedLevel = 2;
                reasons.Add("Both Phone and Email verified");
            }
            else
            {
                if (!hasVerifiedPhone) missingForNext.Add("verification.phone");
                if (!hasVerifiedEmail) missingForNext.Add("verification.email");
            }
        }

        if (computedLevel >= 2)
        {
            if (hasVerifiedAddress)
            {
                computedLevel = 3;
                reasons.Add("Address verified");
            }
            else
            {
                missingForNext.Add("verification.address");
            }
        }

        if (computedLevel >= 3)
        {
            if (hasVerifiedIdentity)
            {
                computedLevel = 4;
                reasons.Add("Official Identity verified");
            }
            else
            {
                missingForNext.Add("verification.identity");
            }
        }

        if (computedLevel >= 4)
        {
            if (hasVerifiedBackground)
            {
                computedLevel = 5;
                reasons.Add("Criminal background check verified");
            }
            else
            {
                missingForNext.Add("verification.background_check");
            }
        }

        var reasonDict = new Dictionary<string, object>
        {
            ["level"] = computedLevel,
            ["criteria"] = reasons,
            ["evaluatedAtUtc"] = DateTimeOffset.UtcNow
        };

        var reasonJson = JsonSerializer.Serialize(reasonDict);

        return new TrustLevelEvaluation(computedLevel, reasonJson, missingForNext);
    }
}
