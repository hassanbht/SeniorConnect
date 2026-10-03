using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Domain;

public sealed class HelpRequestStatusHistory : Entity
{
    private HelpRequestStatusHistory() { }

    [DataClass(DataClass.Operational)]
    public Guid HelpRequestId { get; private set; }

    [DataClass(DataClass.Operational)]
    public HelpRequestStatus FromStatus { get; private set; }

    [DataClass(DataClass.Operational)]
    public HelpRequestStatus ToStatus { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid ChangedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ChangedAtUtc { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Reason { get; private set; }

    public static HelpRequestStatusHistory Create(
        Guid helpRequestId,
        HelpRequestStatus fromStatus,
        HelpRequestStatus toStatus,
        Guid changedByUserId,
        string? reason = null)
    {
        return new HelpRequestStatusHistory
        {
            Id = Guid.CreateVersion7(),
            HelpRequestId = helpRequestId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
            Reason = reason
        };
    }
}
