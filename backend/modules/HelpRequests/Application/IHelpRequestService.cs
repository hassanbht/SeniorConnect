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

    Task<Result<IReadOnlyList<HelpRequestDto>>> GetSeniorHelpRequestsAsync(
        Guid seniorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HelpRequestDto>>> GetOpenHelpRequestsAsync(
        Guid? organizationId = null,
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

    Task<Result<IReadOnlyList<HelpRequestStatusHistoryDto>>> GetStatusHistoryAsync(
        Guid helpRequestId,
        CancellationToken cancellationToken = default);
}
