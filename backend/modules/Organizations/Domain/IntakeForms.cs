using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Domain;

public sealed class OrganizationIntakeForm : Entity, IAuditable
{
    private OrganizationIntakeForm() { }

    [DataClass(DataClass.Operational)]
    public Guid OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public FormType FormType { get; private set; }

    [DataClass(DataClass.PublicProfile)]
    public string Title { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string? Description { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; }

    [DataClass(DataClass.Operational)]
    public int Version { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static OrganizationIntakeForm Create(
        Guid organizationId,
        FormType formType,
        string title,
        string? description,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new OrganizationIntakeForm
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            FormType = formType,
            Title = title,
            Description = description,
            IsActive = true,
            Version = 1,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void UpdateDetails(string title, string? description, Guid? updatedBy = null)
    {
        Title = title;
        Description = description;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Activate(Guid? updatedBy = null)
    {
        IsActive = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Deactivate(Guid? updatedBy = null)
    {
        IsActive = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void IncrementVersion(Guid? updatedBy = null)
    {
        Version++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public enum FormType
{
    Volunteer,
    HelpSeeker
}

public sealed class IntakeFormSection : Entity, IAuditable
{
    private IntakeFormSection() { }

    [DataClass(DataClass.Operational)]
    public Guid FormId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Title { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string? Description { get; private set; }

    [DataClass(DataClass.Operational)]
    public int SortOrder { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static IntakeFormSection Create(
        Guid formId,
        string title,
        string? description,
        int sortOrder,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IntakeFormSection
        {
            Id = Guid.CreateVersion7(),
            FormId = formId,
            Title = title,
            Description = description,
            SortOrder = sortOrder,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void UpdateDetails(string title, string? description, int sortOrder, Guid? updatedBy = null)
    {
        Title = title;
        Description = description;
        SortOrder = sortOrder;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public sealed class IntakeFormField : Entity, IAuditable
{
    private IntakeFormField() { }

    [DataClass(DataClass.Operational)]
    public Guid SectionId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string FieldKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string LabelKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public FieldType FieldType { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsRequired { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? OptionsJson { get; private set; }

    [DataClass(DataClass.Operational)]
    public int SortOrder { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static IntakeFormField Create(
        Guid sectionId,
        string fieldKey,
        string labelKey,
        FieldType fieldType,
        bool isRequired,
        string? optionsJson,
        int sortOrder,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IntakeFormField
        {
            Id = Guid.CreateVersion7(),
            SectionId = sectionId,
            FieldKey = fieldKey,
            LabelKey = labelKey,
            FieldType = fieldType,
            IsRequired = isRequired,
            OptionsJson = optionsJson,
            SortOrder = sortOrder,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void UpdateDetails(
        string fieldKey,
        string labelKey,
        FieldType fieldType,
        bool isRequired,
        string? optionsJson,
        int sortOrder,
        Guid? updatedBy = null)
    {
        FieldKey = fieldKey;
        LabelKey = labelKey;
        FieldType = fieldType;
        IsRequired = isRequired;
        OptionsJson = optionsJson;
        SortOrder = sortOrder;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public enum FieldType
{
    Text,
    Textarea,
    SingleChoice,
    MultiChoice,
    Boolean,
    Date,
    TimeSlots
}

public sealed class IntakeFormSubmission : Entity, IAuditable, IOrganizationScoped
{
    private IntakeFormSubmission() { }

    [DataClass(DataClass.Operational)]
    public Guid FormId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public SubmissionStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string SubmissionDataJson { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public bool CriminalClearanceDeclared { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CriminalClearanceDeclaredAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool GdprConsentAccepted { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? GdprConsentAcceptedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool EventInvitationOptIn { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? DecidedByUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ReviewNotes { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static IntakeFormSubmission Create(
        Guid formId,
        Guid organizationId,
        Guid userId,
        string submissionDataJson,
        bool criminalClearanceDeclared,
        bool gdprConsentAccepted,
        bool eventInvitationOptIn,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IntakeFormSubmission
        {
            Id = Guid.CreateVersion7(),
            FormId = formId,
            OrganizationId = organizationId,
            UserId = userId,
            Status = SubmissionStatus.Submitted,
            SubmissionDataJson = submissionDataJson,
            CriminalClearanceDeclared = criminalClearanceDeclared,
            CriminalClearanceDeclaredAtUtc = criminalClearanceDeclared ? now : null,
            GdprConsentAccepted = gdprConsentAccepted,
            GdprConsentAcceptedAtUtc = gdprConsentAccepted ? now : null,
            EventInvitationOptIn = eventInvitationOptIn,
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void Approve(Guid decidedByUserId, string? reviewNotes = null)
    {
        Status = SubmissionStatus.Approved;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        ReviewNotes = reviewNotes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = decidedByUserId;
    }

    public void Decline(Guid decidedByUserId, string? reviewNotes = null)
    {
        Status = SubmissionStatus.Declined;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        ReviewNotes = reviewNotes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = decidedByUserId;
    }
}

public enum SubmissionStatus
{
    Draft,
    Submitted,
    Approved,
    Declined
}