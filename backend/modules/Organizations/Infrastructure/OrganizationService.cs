using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationService : IOrganizationService
{
    // Field keys of the standard template whose "mandatory" flag also demands the
    // matching timestamped boolean on the submission (BR-ORG-FORM-04/05).
    public const string CriminalClearanceFieldKey = "strafrechtliche_unbescholtenheit";
    public const string GdprConsentFieldKey = "gdpr_consent";

    private readonly IOrganizationsDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IOrganizationAccessGuard _guard;

    private static readonly MemoryCacheEntryOptions OrgCacheOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(60))
        .SetSlidingExpiration(TimeSpan.FromMinutes(15));

    public OrganizationService(IOrganizationsDbContext db, IMemoryCache cache, IOrganizationAccessGuard guard)
    {
        _db = db;
        _cache = cache;
        _guard = guard;
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        Guid callerUserId,
        bool callerIsPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!callerIsPlatformAdmin)
        {
            return Error.CapabilityMissing("PlatformAdmin");
        }

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
            createdBy: callerUserId);

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove("org_active_list");

        return Result<OrganizationDto>.Success(MapOrg(org));
    }

    public async Task<Result<OrganizationDto>> GetOrganizationByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"org_detail_{organizationId}";

        if (!_cache.TryGetValue(cacheKey, out OrganizationDto? dto) || dto is null)
        {
            var org = await _db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

            if (org is null)
            {
                return Error.NotFound("Organization");
            }

            dto = MapOrg(org);
            _cache.Set(cacheKey, dto, OrgCacheOptions);
        }

        return Result<OrganizationDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<OrganizationDto>>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "org_active_list";

        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<OrganizationDto>? dtos) || dtos is null)
        {
            var orgs = await _db.Organizations
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.Status == OrganizationStatus.Active)
                .OrderBy(o => o.Name)
                .ToListAsync(cancellationToken);

            dtos = orgs.Select(MapOrg).ToList();
            _cache.Set(cacheKey, dtos, OrgCacheOptions);
        }

        return Result<IReadOnlyList<OrganizationDto>>.Success(dtos);
    }

    public async Task<Result<BranchDto>> CreateBranchAsync(
        Guid organizationId,
        CreateBranchRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureAdminAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

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
            createdBy: callerUserId);

        _db.OrganizationBranches.Add(branch);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<BranchDto>.Success(MapBranch(branch));
    }

    public async Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var branches = await _db.OrganizationBranches
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var dtos = branches.Select(MapBranch).ToList();
        return Result<IReadOnlyList<BranchDto>>.Success(dtos);
    }

    public async Task<Result<MembershipDto>> AddMembershipAsync(
        Guid organizationId,
        AddMembershipRequest request,
        Guid callerUserId,
        bool callerIsPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        // Only an org Admin grants membership. A platform admin may do so to
        // bootstrap a freshly created organization's first Admin. Nobody can
        // grant themselves a role in an org they do not administer.
        if (!callerIsPlatformAdmin)
        {
            var auth = await _guard.EnsureAdminAsync(callerUserId, organizationId, cancellationToken);
            if (auth.IsFailure) return auth.Error!;
        }

        var existing = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            return new Error("MEMBERSHIP_EXISTS", "User already has a membership in this organization.", ErrorKind.Conflict);
        }

        if (request.BranchId is { } branchId
            && !await _db.OrganizationBranches.AnyAsync(b => b.Id == branchId && b.OrganizationId == organizationId, cancellationToken))
        {
            return Error.NotFound("Branch");
        }

        var membership = OrganizationMembership.Create(
            organizationId: organizationId,
            userId: request.UserId,
            role: request.Role,
            branchId: request.BranchId,
            createdBy: callerUserId);

        membership.Activate();

        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result<MembershipDto>> UpdateMembershipAsync(
        Guid organizationId,
        Guid membershipId,
        UpdateMembershipRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureAdminAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

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
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var memberships = await _db.OrganizationMemberships
            .AsNoTracking()
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
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureAdminAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        if (string.IsNullOrWhiteSpace(key))
        {
            return Error.Validation("Policy key is required.");
        }

        if (!IsJsonDocument(valueJson))
        {
            return Error.Validation("Policy value must be valid JSON.");
        }

        var policy = await _db.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            policy = OrganizationPolicy.Create(organizationId, key, valueJson, callerUserId);
            _db.OrganizationPolicies.Add(policy);
        }
        else
        {
            policy.UpdateValue(valueJson, callerUserId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<PolicyDto>> GetPolicyAsync(
        Guid organizationId,
        string key,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var policy = await _db.OrganizationPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            return Error.NotFound($"Policy '{key}'");
        }

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<PolicyDto>>> GetPoliciesAsync(
        Guid organizationId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var policies = await _db.OrganizationPolicies
            .AsNoTracking()
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
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var form = OrganizationIntakeForm.Create(organizationId, request.FormType, request.Title, request.Description, callerUserId);
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
            .AsNoTracking()
            .Where(f => f.OrganizationId == organizationId && f.FormType == formType && f.IsActive)
            .OrderByDescending(f => f.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        var sections = await _db.IntakeFormSections
            .AsNoTracking()
            .Where(s => s.FormId == form.Id)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var sectionIds = sections.Select(s => s.Id).ToList();
        var fields = await _db.IntakeFormFields
            .AsNoTracking()
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
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

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
        Guid organizationId,
        Guid formId,
        CreateIntakeFormSectionRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        if (!await FormBelongsToOrgAsync(organizationId, formId, cancellationToken))
        {
            return Error.NotFound("IntakeForm");
        }

        var section = IntakeFormSection.Create(formId, request.Title, request.Description, request.SortOrder, callerUserId);
        _db.IntakeFormSections.Add(section);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSectionDto>.Success(MapSection(section));
    }

    public async Task<Result<IntakeFormSectionDto>> UpdateSectionAsync(
        Guid organizationId,
        Guid formId,
        Guid sectionId,
        UpdateIntakeFormSectionRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var section = await FindSectionAsync(organizationId, formId, sectionId, cancellationToken);
        if (section is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        section.UpdateDetails(request.Title, request.Description, request.SortOrder);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormSectionDto>.Success(MapSection(section));
    }

    public async Task<Result> DeleteSectionAsync(
        Guid organizationId,
        Guid formId,
        Guid sectionId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth;

        var section = await FindSectionAsync(organizationId, formId, sectionId, cancellationToken);
        if (section is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        _db.IntakeFormSections.Remove(section);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IntakeFormFieldDto>> AddFieldAsync(
        Guid organizationId,
        Guid formId,
        Guid sectionId,
        CreateIntakeFormFieldRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        if (await FindSectionAsync(organizationId, formId, sectionId, cancellationToken) is null)
        {
            return Error.NotFound("IntakeFormSection");
        }

        var field = IntakeFormField.Create(
            sectionId, request.FieldKey, request.LabelKey, request.FieldType,
            request.IsRequired, request.OptionsJson, request.SortOrder, callerUserId);
        _db.IntakeFormFields.Add(field);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<IntakeFormFieldDto>.Success(MapField(field));
    }

    public async Task<Result<IntakeFormFieldDto>> UpdateFieldAsync(
        Guid organizationId,
        Guid formId,
        Guid sectionId,
        Guid fieldId,
        UpdateIntakeFormFieldRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var field = await FindFieldAsync(organizationId, formId, sectionId, fieldId, cancellationToken);
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
        Guid organizationId,
        Guid formId,
        Guid sectionId,
        Guid fieldId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth;

        var field = await FindFieldAsync(organizationId, formId, sectionId, fieldId, cancellationToken);
        if (field is null)
        {
            return Error.NotFound("IntakeFormField");
        }

        _db.IntakeFormFields.Remove(field);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<bool> FormBelongsToOrgAsync(Guid organizationId, Guid formId, CancellationToken ct) =>
        _db.OrganizationIntakeForms.AnyAsync(f => f.Id == formId && f.OrganizationId == organizationId, ct);

    private async Task<IntakeFormSection?> FindSectionAsync(Guid organizationId, Guid formId, Guid sectionId, CancellationToken ct)
    {
        if (!await FormBelongsToOrgAsync(organizationId, formId, ct)) return null;

        return await _db.IntakeFormSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.FormId == formId, ct);
    }

    private async Task<IntakeFormField?> FindFieldAsync(Guid organizationId, Guid formId, Guid sectionId, Guid fieldId, CancellationToken ct)
    {
        if (await FindSectionAsync(organizationId, formId, sectionId, ct) is null) return null;

        return await _db.IntakeFormFields
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.SectionId == sectionId, ct);
    }

    public async Task<Result<IntakeFormSubmissionDto>> SubmitIntakeFormAsync(
        Guid formId,
        Guid organizationId,
        SubmitIntakeFormRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.OrganizationIntakeForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formId && f.OrganizationId == organizationId && f.IsActive, cancellationToken);

        if (form is null)
        {
            return Error.NotFound("IntakeForm");
        }

        var sectionIds = await _db.IntakeFormSections
            .AsNoTracking()
            .Where(s => s.FormId == formId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var requiredFields = await _db.IntakeFormFields
            .AsNoTracking()
            .Where(f => sectionIds.Contains(f.SectionId) && f.IsRequired)
            .Select(f => new RequiredField(f.FieldKey, f.LabelKey, f.FieldType))
            .ToListAsync(cancellationToken);

        var validation = ValidateSubmission(request, requiredFields);
        if (validation.IsFailure) return validation.Error!;

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

    public sealed record RequiredField(string FieldKey, string LabelKey, FieldType FieldType);

    /// <summary>
    /// P2-38 / BR-ORG-FORM-02/04/05. A required field is satisfied only by a
    /// meaningful value for its type: a Boolean must be <c>true</c> (a
    /// mandatory declaration answered "no" is not an answer), a MultiChoice /
    /// TimeSlots needs at least one element, text must be non-blank.
    /// </summary>
    public static Result ValidateSubmission(SubmitIntakeFormRequest request, IReadOnlyCollection<RequiredField> requiredFields)
    {
        if (string.IsNullOrWhiteSpace(request.SubmissionDataJson))
        {
            return FieldError("INTAKE_SUBMISSION_INVALID", "Submission data is required.", null);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(request.SubmissionDataJson);
        }
        catch (JsonException)
        {
            return FieldError("INTAKE_SUBMISSION_INVALID", "Submission data is not valid JSON.", null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return FieldError("INTAKE_SUBMISSION_INVALID", "Submission data must be a JSON object.", null);
            }

            foreach (var field in requiredFields)
            {
                if (!root.TryGetProperty(field.FieldKey, out var value) || !IsSatisfied(field.FieldType, value))
                {
                    return FieldError("VALIDATION_FAILED", $"Required field '{field.LabelKey}' is missing.", field.FieldKey);
                }
            }
        }

        var keys = requiredFields.Select(f => f.FieldKey).ToHashSet(StringComparer.Ordinal);
        if (keys.Contains(CriminalClearanceFieldKey) && !request.CriminalClearanceDeclared)
        {
            return FieldError("VALIDATION_FAILED", "The criminal clearance declaration is mandatory for this form.", CriminalClearanceFieldKey);
        }

        if (keys.Contains(GdprConsentFieldKey) && !request.GdprConsentAccepted)
        {
            return FieldError("VALIDATION_FAILED", "GDPR consent is mandatory for this form.", GdprConsentFieldKey);
        }

        return Result.Success();
    }

    private static bool IsSatisfied(FieldType type, JsonElement value) => type switch
    {
        FieldType.Boolean => value.ValueKind == JsonValueKind.True,
        FieldType.MultiChoice or FieldType.TimeSlots =>
            value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0,
        _ => value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            _ => true,
        },
    };

    private static Result FieldError(string code, string detail, string? fieldKey) =>
        new Error(code, detail, ErrorKind.Validation,
            fieldKey is null ? null : new Dictionary<string, object> { ["field"] = fieldKey });

    private static bool IsJsonDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<Result<IReadOnlyList<IntakeFormSubmissionDto>>> GetSubmissionsAsync(
        Guid formId,
        Guid organizationId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        if (!await FormBelongsToOrgAsync(organizationId, formId, cancellationToken))
        {
            return Error.NotFound("IntakeForm");
        }

        var submissions = await _db.IntakeFormSubmissions
            .AsNoTracking()
            .Where(s => s.FormId == formId && s.OrganizationId == organizationId)
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
        var auth = await _guard.EnsureStaffAsync(decidedByUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth.Error!;

        var submission = await _db.IntakeFormSubmissions
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.FormId == formId && s.OrganizationId == organizationId, cancellationToken);

        if (submission is null)
        {
            return Error.NotFound("IntakeFormSubmission");
        }

        // Four-eyes: staff never decide their own application.
        if (submission.UserId == decidedByUserId)
        {
            return new Error("SELF_DECISION_FORBIDDEN", "You cannot decide on your own submission.", ErrorKind.Forbidden);
        }

        if (submission.Status != SubmissionStatus.Submitted)
        {
            return Error.InvalidStateTransition(
                submission.Status.ToString(),
                (request.Approve ? SubmissionStatus.Approved : SubmissionStatus.Declined).ToString());
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

    public async Task<Result> ActivateStandardTemplateAsync(
        Guid organizationId,
        ActivateStandardTemplateRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _guard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (auth.IsFailure) return auth;

        Guid? createdBy = callerUserId;

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
            "Standard: Interesse für Freiwilligentätigkeit",
            "Standard-Aufnahmeformular für Freiwillige",
            createdBy);

        _db.OrganizationIntakeForms.Add(form);
        await _db.SaveChangesAsync(cancellationToken);

        // Create sections and fields for the standard template
        await SeedStandardTemplateAsync(form.Id, createdBy, cancellationToken);

        return Result.Success();
    }

    private async Task SeedStandardTemplateAsync(Guid formId, Guid? createdBy, CancellationToken cancellationToken)
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
            .AsNoTracking()
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

