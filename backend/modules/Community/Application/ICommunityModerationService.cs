namespace SeniorConnect.Modules.Community.Application;

/// <summary>
/// Platform moderation abstraction for community content.
/// MVP implementation: rule-based, local, no external LLM calls.
/// Future implementation: AI-based, behind the same interface.
/// </summary>
public interface ICommunityModerationService
{
    Task<ModerationResult> EvaluateAsync(ModerationContext context, CancellationToken ct = default);
}

public sealed record ModerationContext(
    string ContentType, // "post", "comment", "announcement", "event_description", "group_description", "message"
    string Content,
    Guid? AuthorUserId,
    Guid? GroupId);

public sealed record ModerationResult(
    ModerationDecision Decision,
    double Confidence,
    string Category,
    string? Reason,
    string Version,
    DateTimeOffset EvaluatedAtUtc);

public enum ModerationDecision
{
    Allow,
    Flag,
    HideTemporarily,
    RateLimit,
    EscalateToHuman
}
