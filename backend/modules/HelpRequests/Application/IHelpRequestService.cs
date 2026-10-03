using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public interface IHelpRequestService
{
    Task<Result<HelpRequestDto>> CreateHelpRequestAsync(
        Guid createdByUserId,
        Guid seniorUserId,
        CreateHelpRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> GetHelpRequestByIdAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestLocationDto>> GetHelpRequestLocationAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HelpRequestDto>>> GetSeniorHelpRequestsAsync(
        Guid seniorUserId,
        CancellationToken cancellationToken = default);

    /// <param name="viewerUserId">Phase 4: requests of people who blocked the viewer (or are restricted towards them) are left out.</param>
    Task<Result<IReadOnlyList<HelpRequestDto>>> GetOpenHelpRequestsAsync(
        Guid? organizationId = null,
        Guid? viewerUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> AcceptHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        AcceptHelpRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> CheckInHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> CompleteHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        CompleteHelpRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> CancelHelpRequestAsync(
        Guid helpRequestId,
        Guid cancelledByUserId,
        CancelHelpRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> MarkNoShowAsync(
        Guid helpRequestId,
        Guid reportedByUserId,
        NoShowHelpRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HelpRequestDto>> DisputeNoShowAsync(
        Guid helpRequestId,
        Guid disputedByUserId,
        DisputeNoShowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>BR-HELP-06: coordinator of the request's organization resolves a disputed no-show, once.</summary>
    Task<Result<HelpRequestDto>> ResolveNoShowDisputeAsync(
        Guid helpRequestId,
        Guid resolvedByUserId,
        ResolveNoShowDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HelpRequestStatusHistoryDto>>> GetStatusHistoryAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);

    // --- ADR-026 Option C (approved 2026-09-30; legal validation pending) ----

    /// <summary>Senior / authorized representative grants the visit address to the current assignee.</summary>
    Task<Result> GrantVisitAddressAsync(
        Guid helpRequestId,
        Guid grantedByUserId,
        GrantVisitAddressRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Grantor (or the organization coordinator of record) revokes until check-in.</summary>
    Task<Result> RevokeVisitAddressAsync(
        Guid helpRequestId,
        Guid revokedByUserId,
        RevokeVisitAddressRequest request,
        CancellationToken cancellationToken = default);
}
