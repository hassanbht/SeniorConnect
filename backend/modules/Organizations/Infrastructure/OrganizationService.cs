using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationService : IOrganizationService
{
    private readonly IOrganizationsDbContext _db;

    public OrganizationService(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("Organization name is required.");
        }

        var org = Organization.Create(
            name: request.Name.Trim(),
            type: request.Type,
            legalName: request.LegalName?.Trim(),
            supportEmail: request.SupportEmail?.Trim().ToLowerInvariant(),
            supportPhone: request.SupportPhone?.Trim(),
            createdBy: createdBy);

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<OrganizationDto>.Success(MapOrg(org));
    }

    public async Task<Result<OrganizationDto>> GetOrganizationByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        return Result<OrganizationDto>.Success(MapOrg(org));
    }

    public async Task<Result<IReadOnlyList<OrganizationDto>>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await _db.Organizations
            .Where(o => !o.IsDeleted && o.Status == OrganizationStatus.Active)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var dtos = orgs.Select(MapOrg).ToList();
        return Result<IReadOnlyList<OrganizationDto>>.Success(dtos);
    }

    public async Task<Result<BranchDto>> CreateBranchAsync(
        Guid organizationId,
        CreateBranchRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("Branch name is required.");
        }

        var branch = OrganizationBranch.Create(
            organizationId: organizationId,
            name: request.Name.Trim(),
            address: request.Address?.Trim(),
            postalCode: request.PostalCode?.Trim(),
            city: request.City?.Trim(),
            latitude: request.Latitude,
            longitude: request.Longitude,
            createdBy: createdBy);

        _db.OrganizationBranches.Add(branch);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<BranchDto>.Success(MapBranch(branch));
    }

    public async Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var branches = await _db.OrganizationBranches
            .Where(b => b.OrganizationId == organizationId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var dtos = branches.Select(MapBranch).ToList();
        return Result<IReadOnlyList<BranchDto>>.Success(dtos);
    }

    public async Task<Result<MembershipDto>> AddMembershipAsync(
        Guid organizationId,
        AddMembershipRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        var existing = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            return new Error("MEMBERSHIP_EXISTS", "User already has a membership in this organization.", ErrorKind.Conflict);
        }

        var membership = OrganizationMembership.Create(
            organizationId: organizationId,
            userId: request.UserId,
            role: request.Role,
            branchId: request.BranchId,
            createdBy: createdBy);

        membership.Activate();

        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result<MembershipDto>> UpdateMembershipAsync(
        Guid organizationId,
        Guid membershipId,
        UpdateMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.OrganizationId == organizationId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("Membership");
        }

        membership.ChangeRole(request.Role);
        if (request.Status == MembershipStatus.Active) membership.Activate();
        else if (request.Status == MembershipStatus.Suspended) membership.Suspend();
        else if (request.Status == MembershipStatus.Left) membership.Leave();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result<IReadOnlyList<MembershipDto>>> GetMembershipsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = memberships.Select(MapMembership).ToList();
        return Result<IReadOnlyList<MembershipDto>>.Success(dtos);
    }

    public async Task<Result<PolicyDto>> SetPolicyAsync(
        Guid organizationId,
        string key,
        string valueJson,
        Guid? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            policy = OrganizationPolicy.Create(organizationId, key, valueJson, updatedBy);
            _db.OrganizationPolicies.Add(policy);
        }
        else
        {
            policy.UpdateValue(valueJson, updatedBy);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<PolicyDto>> GetPolicyAsync(
        Guid organizationId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            return Error.NotFound($"Policy '{key}'");
        }

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<PolicyDto>>> GetPoliciesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var policies = await _db.OrganizationPolicies
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.PolicyKey)
            .ToListAsync(cancellationToken);

        var dtos = policies.Select(p => new PolicyDto(p.PolicyKey, p.PolicyValueJson, p.UpdatedAtUtc)).ToList();
        return Result<IReadOnlyList<PolicyDto>>.Success(dtos);
    }

    // Intake Forms (ADR-021, BR-ORG-FORM)
    public async Task<Result<OrganizationIntakeFormDto>> CreateIntakeFormAsync(
        Guid organizationId,
        CreateIntakeFormRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        var form = OrganizationIntakeForm.Create(organizationId, request.FormType, request.Title, request.Description, createdBy);
        _db.OrganizationIntakeForms.Add(form);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<OrganizationIntakeFormDto>.Success(MapIntakeForm(form));
    }

    public async Task<Result<OrganizationIntakeFormDetailDto>> GetIntakeFormAsync(
        Guid organizationId,
        FormType formType,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .Where(f => f.OrganizationId == organizationId && f.FormType == formType && f.IsActive)
            .OrderByDescending(f => f.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        var sections = await _db.IntakeFormSections
            .Where(s => s.FormId == form.Id)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var sectionIds = sections.Select(s => s.Id).ToList();
        var fields = await _db.IntakeFormFields
            .Where(f => sectionIds.Contains(f.SectionId))
            .OrderBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);

        var sectionDtos = sections
            .Select(s => new IntakeFormSectionDetailDto(
                s.Id,
                s.FormId,
                s.Title,
                s.Description,
                s.SortOrder,
                fields.Where(f => f.SectionId == s.Id).Select(MapField).ToList()))
            .ToList();

        return Result<OrganizationIntakeFormDetailDto>.Success(new OrganizationIntakeFormDetailDto(
            form.Id,
            form.OrganizationId,
            form.FormType,
            form.Title,
            form.Description,
            form.IsActive,
            form.Version,
            form.CreatedAtUtc,
            sectionDtos));
    }

    public async Task<Result<OrganizationIntakeFormDto>> UpdateIntakeFormAsync(
        Guid organizationId,
        Guid formId,
        UpdateIntakeFormRequest request,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .FirstOrDefaultAsync(f => f.Id == formId && f.OrganizationId == organizationId, cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        form.UpdateDetails(request.Title, request.Description);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<OrganizationIntakeFormDto>.Success(MapIntakeForm(form));
    }

    public async Task<Result<IntakeFormSectionDto>> AddSectionAsync(
        Guid formId,
        CreateIntakeFormSectionRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        var section = IntakeFormSection.Create(formId, request.Title, request.Description, request.SortOrder, createdBy);
        _db.IntakeFormSections.Add(section);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSectionDto>.Success(MapSection(section));
    }

    public async Task<Result<IntakeFormSectionDto>> UpdateSectionAsync(
        Guid formId,
        Guid sectionId,
        UpdateIntakeFormSectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var section = await _db.IntakeFormSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.FormId == formId, cancellationToken);

        if (section is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        section.UpdateDetails(request.Title, request.Description, request.SortOrder);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSectionDto>.Success(MapSection(section));
    }

    public async Task<Result> DeleteSectionAsync(
        Guid formId,
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        var section = await _db.IntakeFormSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.FormId == formId, cancellationToken);

        if (section is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        _db.IntakeFormSections.Remove(section);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IntakeFormFieldDto>> AddFieldAsync(
        Guid sectionId,
        CreateIntakeFormFieldRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var section = await _db.IntakeFormSections
            .FirstOrDefaultAsync(s => s.Id == sectionId, cancellationToken);

        if (section is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        var field = IntakeFormField.Create(
            sectionId, request.FieldKey, request.LabelKey, request.FieldType,
            request.IsRequired, request.OptionsJson, request.SortOrder, createdBy);
        _db.IntakeFormFields.Add(field);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormFieldDto>.Success(MapField(field));
    }

    public async Task<Result<IntakeFormFieldDto>> UpdateFieldAsync(
        Guid sectionId,
        Guid fieldId,
        UpdateIntakeFormFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        var field = await _db.IntakeFormFields
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.SectionId == sectionId, cancellationToken);

        if (field is null)
        {
            return Error.NotFound("IntakeFormField");
        }

        field.UpdateDetails(
            request.FieldKey, request.LabelKey, request.FieldType,
            request.IsRequired, request.OptionsJson, request.SortOrder);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormFieldDto>.Success(MapField(field));
    }

    public async Task<Result> DeleteFieldAsync(
        Guid sectionId,
        Guid fieldId,
        CancellationToken cancellationToken = default)
    {
        var field = await _db.IntakeFormFields
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.SectionId == sectionId, cancellationToken);

        if (field is null)
        {
            return Error.NotFound("IntakeFormField");
        }

        _db.IntakeFormFields.Remove(field);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IntakeFormSubmissionDto>> SubmitIntakeFormAsync(
        Guid formId,
        Guid organizationId,
        SubmitIntakeFormRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .FirstOrDefaultAsync(f => f.Id == formId && f.OrganizationId == organizationId && f.IsActive, cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        // Validate required fields
        var fields = await _db.IntakeFormFields
            .Where(f => f.SectionId == _db.IntakeFormSections
                .Where(s => s.FormId == formId)
                .Select(s => s.Id)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        // Better approach: get all fields for the form
        var sectionIds = await _db.IntakeFormSections
            .Where(s => s.FormId == formId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var formFields = await _db.IntakeFormFields
            .Where(f => sectionIds.Contains(f.SectionId))
            .ToListAsync(cancellationToken);

        var submissionData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmissionDataJson);

        foreach (var field in formFields.Where(f => f.IsRequired))
        {
            if (!submissionData!.TryGetValue(field.FieldKey, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
            {
                return new Error("VALIDATION_FAILED", $"Required field '{field.LabelKey}' is missing.", ErrorKind.Validation);
            }
        }

        var submission = IntakeFormSubmission.Create(
            formId, organizationId, userId,
            request.SubmissionDataJson,
            request.CriminalClearanceDeclared,
            request.GdprConsentAccepted,
            request.EventInvitationOptIn,
            userId);

        _db.IntakeFormSubmissions.Add(submission);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSubmissionDto>.Success(MapSubmission(submission));
    }

    public async Task<Result<IReadOnlyList<IntakeFormSubmissionDto>>> GetSubmissionsAsync(
        Guid formId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .FirstOrDefaultAsync(f => f.Id == formId && f.OrganizationId == organizationId, cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        var submissions = await _db.IntakeFormSubmissions
            .Where(s => s.FormId == formId)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = submissions.Select(MapSubmission).ToList();
        return Result<IReadOnlyList<IntakeFormSubmissionDto>>.Success(dtos);
    }

    public async Task<Result<IntakeFormSubmissionDto>> DecideSubmissionAsync(
        Guid formId,
        Guid organizationId,
        Guid submissionId,
        DecideIntakeFormSubmissionRequest request,
        Guid decidedByUserId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _db.IntakeFormSubmissions
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.FormId == formId && s.OrganizationId == organizationId, cancellationToken);

        if (submission is null)
        {
            return Error.NotFound("IntakeFormSubmission");
        }

        if (request.Approve)
        {
            submission.Approve(decidedByUserId, request.ReviewNotes);
        }
        else
        {
            submission.Decline(decidedByUserId, request.ReviewNotes);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSubmissionDto>.Success(MapSubmission(submission));
    }

    public async Task<Result> ActivateFwzTemplateAsync(
        Guid organizationId,
        ActivateFwzTemplateRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        // Check if template already exists
        var existing = await _db.OrganizationIntakeForms
            .FirstOrDefaultAsync(f => f.OrganizationId == organizationId && f.FormType == request.FormType && f.IsActive, cancellationToken);

        if (existing is not null)
        {
            // Deactivate existing
            existing.Deactivate(createdBy);
        }

        var form = OrganizationIntakeForm.Create(
            organizationId,
            request.FormType,
            "FWZ Innsbruck-Land: Interesse für Freiwilligentätigkeit",
            "Standard-Aufnahmeformular für Freiwillige nach Vorlage FWZ Innsbruck-Land",
            createdBy);

        _db.OrganizationIntakeForms.Add(form);
        await _db.SaveChangesAsync(cancellationToken);

        // Create sections and fields for FWZ template
        await SeedFwzTemplateAsync(form.Id, createdBy, cancellationToken);

        return Result.Success();
    }

    private async Task SeedFwzTemplateAsync(Guid formId, Guid? createdBy, CancellationToken cancellationToken)
    {
        // Section 1: Bereiche
        var section1 = IntakeFormSection.Create(formId, "Bereiche", "In welchen Bereichen möchten Sie sich engagieren?", 1, createdBy);
        _db.IntakeFormSections.Add(section1);

        // Section 2: Personengruppen
        var section2 = IntakeFormSection.Create(formId, "Personengruppen", "Mit welchen Personengruppen möchten Sie arbeiten?", 2, createdBy);
        _db.IntakeFormSections.Add(section2);

        // Section 3: Zeitaufwand
        var section3 = IntakeFormSection.Create(formId, "Zeitaufwand", "Wie viel Zeit können Sie einbringen?", 3, createdBy);
        _db.IntakeFormSections.Add(section3);

        // Section 4: Fähigkeiten/Anmerkungen
        var section4 = IntakeFormSection.Create(formId, "Fähigkeiten & Anmerkungen", "Haben Sie besondere Fähigkeiten oder Anmerkungen?", 4, createdBy);
        _db.IntakeFormSections.Add(section4);

        // Section 5: Strafrechtliche Unbescholtenheit
        var section5 = IntakeFormSection.Create(formId, "Strafrechtliche Unbescholtenheit", "Bestätigung der Unbescholtenheit", 5, createdBy);
        _db.IntakeFormSections.Add(section5);

        // Section 6: GDPR Einwilligung
        var section6 = IntakeFormSection.Create(formId, "Datenschutz", "Einwilligung zur Datenverarbeitung", 6, createdBy);
        _db.IntakeFormSections.Add(section6);

        await _db.SaveChangesAsync(cancellationToken);

        // Now add fields for each section
        var sections = await _db.IntakeFormSections
            .Where(s => s.FormId == formId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var section1Id = sections[0].Id;
        var section2Id = sections[1].Id;
        var section3Id = sections[2].Id;
        var section4Id = sections[3].Id;
        var section5Id = sections[4].Id;
        var section6Id = sections[5].Id;

        // Define arrays as local variables to avoid CA1861
        var bereicheArray = new[] { "Soziales", "Natur", "E-Volunteering", "Klima und Nachhaltigkeit", "Handwerkliches / Kreatives", "Kunst und Kultur", "Freiwilligenpool", "Lernbetreuung" };
        var personengruppenArray = new[] { "Geflüchtete / Personen mit Migrationshintergrund", "Familien", "Senior:innen", "Menschen mit Behinderung", "Kinder und Jugendliche", "Sonstige" };
        var zeitaufwandArray = new[] { "einmalig", "regelmäßig (pro Woche)", "regelmäßig (pro Monat)", "regelmäßig (pro Jahr)", "Stundenanzahl", "Tageszeit", "Wochentag(e)", "Flexibel", "WhatsApp-Zustimmung zur Kontaktaufnahme" };

        var bereicheOptions = System.Text.Json.JsonSerializer.Serialize(bereicheArray);
        var personengruppenOptions = System.Text.Json.JsonSerializer.Serialize(personengruppenArray);
        var zeitaufwandOptions = System.Text.Json.JsonSerializer.Serialize(zeitaufwandArray);

        _db.IntakeFormFields.Add(IntakeFormField.Create(section1Id, "bereiche", "intake.field.bereiche", FieldType.MultiChoice, true, bereicheOptions, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section2Id, "personengruppen", "intake.field.personengruppen", FieldType.MultiChoice, true, personengruppenOptions, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section3Id, "zeitaufwand", "intake.field.zeitaufwand", FieldType.MultiChoice, true, zeitaufwandOptions, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section4Id, "faehigkeiten", "intake.field.faehigkeiten", FieldType.Textarea, false, null, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section5Id, "strafrechtliche_unbescholtenheit", "intake.field.strafrechtliche_unbescholtenheit", FieldType.Boolean, true, null, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section6Id, "gdpr_consent", "intake.field.gdpr_consent", FieldType.Boolean, true, null, 1, createdBy));
        _db.IntakeFormFields.Add(IntakeFormField.Create(section6Id, "event_invitation_opt_in", "intake.field.event_invitation_opt_in", FieldType.Boolean, false, null, 2, createdBy));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static OrganizationIntakeFormDto MapIntakeForm(OrganizationIntakeForm f) => new(
        Id: f.Id,
        OrganizationId: f.OrganizationId,
        FormType: f.FormType,
        Title: f.Title,
        Description: f.Description,
        IsActive: f.IsActive,
        Version: f.Version,
        CreatedAtUtc: f.CreatedAtUtc);

    private static IntakeFormSectionDto MapSection(IntakeFormSection s) => new(
        Id: s.Id,
        FormId: s.FormId,
        Title: s.Title,
        Description: s.Description,
        SortOrder: s.SortOrder);

    private static IntakeFormFieldDto MapField(IntakeFormField f) => new(
        Id: f.Id,
        SectionId: f.SectionId,
        FieldKey: f.FieldKey,
        LabelKey: f.LabelKey,
        FieldType: f.FieldType,
        IsRequired: f.IsRequired,
        OptionsJson: f.OptionsJson,
        SortOrder: f.SortOrder);

    private static IntakeFormSubmissionDto MapSubmission(IntakeFormSubmission s) => new(
        Id: s.Id,
        FormId: s.FormId,
        OrganizationId: s.OrganizationId,
        UserId: s.UserId,
        Status: s.Status,
        SubmissionDataJson: s.SubmissionDataJson,
        CriminalClearanceDeclared: s.CriminalClearanceDeclared,
        CriminalClearanceDeclaredAtUtc: s.CriminalClearanceDeclaredAtUtc,
        GdprConsentAccepted: s.GdprConsentAccepted,
        GdprConsentAcceptedAtUtc: s.GdprConsentAcceptedAtUtc,
        EventInvitationOptIn: s.EventInvitationOptIn,
        SubmittedAtUtc: s.SubmittedAtUtc,
        DecidedAtUtc: s.DecidedAtUtc,
        DecidedByUserId: s.DecidedByUserId,
        ReviewNotes: s.ReviewNotes,
        CreatedAtUtc: s.CreatedAtUtc);

    private static OrganizationDto MapOrg(Organization o) => new(
        Id: o.Id,
        Name: o.Name,
        LegalName: o.LegalName,
        Type: o.Type,
        Status: o.Status,
        SupportEmail: o.SupportEmail,
        SupportPhone: o.SupportPhone,
        CreatedAtUtc: o.CreatedAtUtc);

    private static BranchDto MapBranch(OrganizationBranch b) => new(
        Id: b.Id,
        OrganizationId: b.OrganizationId!.Value,
        Name: b.Name,
        Address: b.Address,
        PostalCode: b.PostalCode,
        City: b.City,
        Latitude: b.Latitude,
        Longitude: b.Longitude,
        IsActive: b.IsActive);

    private static MembershipDto MapMembership(OrganizationMembership m) => new(
        Id: m.Id,
        OrganizationId: m.OrganizationId!.Value,
        BranchId: m.BranchId,
        UserId: m.UserId,
        Role: m.Role,
        Status: m.Status,
        JoinedAtUtc: m.JoinedAtUtc,
        CreatedAtUtc: m.CreatedAtUtc);
}
