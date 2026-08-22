using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

public sealed class VolunteerApplicationStep : Entity
{
    private VolunteerApplicationStep() { }

    [DataClass(DataClass.Operational)]
    public Guid ApplicationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public ApplicationStepType Step { get; private set; }

    [DataClass(DataClass.Operational)]
    public int SortOrder { get; private set; }

    [DataClass(DataClass.Operational)]
    public StepStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public int SlaDays { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? OpenedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CompletedByUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Note { get; private set; }

    public static VolunteerApplicationStep Create(
        Guid applicationId,
        ApplicationStepType step,
        int sortOrder,
        int slaDays = 14)
    {
        return new VolunteerApplicationStep
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Step = step,
            SortOrder = sortOrder,
            Status = StepStatus.NotStarted,
            SlaDays = slaDays
        };
    }

    public void Start()
    {
        Status = StepStatus.InProgress;
        OpenedAtUtc ??= DateTimeOffset.UtcNow;
    }

    public void Complete(Guid completedByUserId, string? note = null)
    {
        Status = StepStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        CompletedByUserId = completedByUserId;
        Note = note;
    }

    public void Block(string? note = null)
    {
        Status = StepStatus.Blocked;
        Note = note;
    }

    public void Skip(string? note = null)
    {
        Status = StepStatus.Skipped;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Note = note;
    }
}

public enum ApplicationStepType
{
    Interview,
    BackgroundCheck,
    ConfidentialityAgreement,
    Briefing,
    Approval
}

public enum StepStatus
{
    NotStarted,
    InProgress,
    Completed,
    Blocked,
    Skipped
}
