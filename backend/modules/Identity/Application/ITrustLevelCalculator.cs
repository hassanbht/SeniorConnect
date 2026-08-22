using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface ITrustLevelCalculator
{
    TrustLevelEvaluation Evaluate(User user, IReadOnlyList<Verification> verifications);
}

public sealed record TrustLevelEvaluation(
    short Level,
    string ReasonJson,
    IReadOnlyList<string> MissingForNextLevel);
