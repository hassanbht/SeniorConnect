namespace SeniorConnect.Modules.Community.Application;

/// <summary>
/// Legacy adapter that keeps contextual thread messages working with the new
/// <see cref="ICommunityModerationService"/> abstraction. New code should use
/// <see cref="ICommunityModerationService"/> directly.
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
