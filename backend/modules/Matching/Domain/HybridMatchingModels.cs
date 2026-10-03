namespace SeniorConnect.Modules.Matching.Domain;

/// <summary>
/// Domain model for AI-assisted hybrid matching proposal (ADR-014).
/// AI proposes candidate matches with confidence scores and explainable rationale,
/// while humans (coordinators or seniors) retain final assignment authority.
/// </summary>
public sealed record HybridMatchingProposal(
    Guid HelpRequestId,
    Guid CandidateVolunteerUserId,
    double CombinedScore,
    double RuleBasedScore,
    double PredictedCompletionProbability,
    ScoreBreakdown Breakdown,
    string AiRecommendationReason,
    bool RequiresManualCoordinatorApproval);

public sealed record HybridMatchingRequest(
    Guid HelpRequestId,
    bool EnableCompletionPrediction = true,
    double MinConfidenceThreshold = 0.50);
