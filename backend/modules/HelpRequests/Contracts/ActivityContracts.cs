using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Contracts;

// Other modules may reference ONLY this namespace. Never Domain, never
// Application, never Infrastructure. An architecture test enforces it.

public sealed record LogActivityRequest(
    Guid? OrganizationId,
    Guid VolunteerUserId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    string LocationType,
    string TransportMode,
    string InsuranceContext,
    string? Notes);

public sealed record ActivityResponse(
    Guid Id,
    Guid? OrganizationId,
    Guid VolunteerUserId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    string Status,
    string InsuranceContext,
    string TransportMode,
    bool RequiresInsuranceResolution,
    DateTimeOffset LoggedAtUtc,
    DateTimeOffset? ConfirmedAtUtc);

/// <summary>
/// BR-SCOPE-03. A blocked category returns this instead of creating anything.
/// The response is a referral, not an error the user should feel bad about.
/// </summary>
public sealed record ReferralResponse(
    string ReferralGroup,
    string MessageKey,
    IReadOnlyList<ReferralProvider> Providers);

public sealed record ReferralProvider(string Name, string? Phone, string? Website);

/// <summary>Consumed by the Reporting module. Aggregate only.</summary>
public interface IVolunteerHoursReader
{
    Task<IReadOnlyList<VolunteerHoursRow>> GetMonthlyAsync(
        Guid organizationId, DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record VolunteerHoursRow(
    Guid VolunteerUserId, DateOnly Month, int ActivityCount, decimal Hours);
