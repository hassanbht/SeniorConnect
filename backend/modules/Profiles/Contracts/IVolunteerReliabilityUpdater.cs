namespace SeniorConnect.Modules.Profiles.Contracts;

/// <summary>
/// P3-20: lets other modules (HelpRequests) record a behaviour outcome
/// against a volunteer's reliability without depending on the Profiles
/// domain internals directly — same cross-module pattern as
/// Identity.Contracts.ITrustLevelReader.
/// </summary>
public interface IVolunteerReliabilityUpdater
{
    /// <summary>
    /// Nudges reliability toward 1.0 (completed) or 0.0 (uncontested
    /// no-show). Returns the score as it was BEFORE this update, so a
    /// caller can restore it verbatim if the outcome is later disputed
    /// successfully (P3-19). Returns null if the volunteer has no profile.
    /// </summary>
    Task<decimal?> RecordOutcomeAsync(Guid volunteerUserId, bool wasReliable, CancellationToken cancellationToken = default);

    /// <summary>Restores a previously snapshotted score verbatim (P3-19: a successful dispute reverts the score, not just re-nudges it).</summary>
    Task RestoreScoreAsync(Guid volunteerUserId, decimal? score, CancellationToken cancellationToken = default);
}
