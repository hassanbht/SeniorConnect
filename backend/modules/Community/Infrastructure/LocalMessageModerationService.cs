using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

/// <summary>
/// Adapter that implements the legacy <see cref="IMessageModerationService"/>
/// on top of the new <see cref="ICommunityModerationService"/> abstraction.
/// </summary>
public sealed class LocalMessageModerationService : IMessageModerationService
{
    private readonly ICommunityModerationService _communityModeration;

    public LocalMessageModerationService(ICommunityModerationService communityModeration)
    {
        _communityModeration = communityModeration;
    }

    public ModerationVerdict Screen(string content)
    {
        var result = _communityModeration.EvaluateAsync(new ModerationContext(
            ContentType: "message",
            Content: content,
            AuthorUserId: null,
            GroupId: null)).GetAwaiter().GetResult();

        return result.Decision switch
        {
            ModerationDecision.Allow => ModerationVerdict.Clean,
            _ => ModerationVerdict.Flag(result.Reason ?? "Inhalt wird geprüft.")
        };
    }
}
