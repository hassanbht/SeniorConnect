namespace SeniorConnect.Modules.Community.Application;

/// <summary>
/// BR-COMM-05 / ADR-020: any message visible to more than its author and one
/// recipient is screened before it becomes visible to anyone else. The
/// implementation must be local/lightweight — never a cloud LLM call on
/// personal data.
/// </summary>
public interface IMessageModerationService
{
    ModerationVerdict Screen(string content);
}

public sealed record ModerationVerdict(bool IsFlagged, string? Reason)
{
    public static ModerationVerdict Clean { get; } = new(false, null);

    public static ModerationVerdict Flag(string reason) => new(true, reason);
}
