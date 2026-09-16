using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

// P5-04 gate: recurring events materialize occurrences lazily (never one row
// per occurrence) — the only thing that must persist per-occurrence is a
// cancellation, since everything else is computed from
// CommunityEvent.ComputeOccurrenceStartsUtc(). Cancelling one occurrence
// must never cancel the series (that stays CommunityEvent.Cancel()).
public sealed class CommunityEventOccurrenceCancellation : Entity
{
    private CommunityEventOccurrenceCancellation() { }

    [DataClass(DataClass.Operational)]
    public Guid EventId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset OccurrenceStartUtc { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Reason { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public Guid CancelledByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CancelledAtUtc { get; private set; }

    public static Result<CommunityEventOccurrenceCancellation> Create(
        Guid eventId, DateTimeOffset occurrenceStartUtc, string reason, Guid cancelledByUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("A cancellation reason is required.");
        }

        return Result<CommunityEventOccurrenceCancellation>.Success(new CommunityEventOccurrenceCancellation
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            OccurrenceStartUtc = occurrenceStartUtc,
            Reason = reason.Trim(),
            CancelledByUserId = cancelledByUserId,
            CancelledAtUtc = DateTimeOffset.UtcNow
        });
    }
}
