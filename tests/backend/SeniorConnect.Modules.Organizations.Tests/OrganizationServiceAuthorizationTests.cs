using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;
using static SeniorConnect.Modules.Organizations.Tests.OrganizationAccessGuardTests;

namespace SeniorConnect.Modules.Organizations.Tests;

public sealed class OrganizationServiceAuthorizationTests : IDisposable
{
    private readonly SeniorConnectDbContext _db = CreateDb();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly OrganizationService _service;
    private readonly Organization _org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _coordinator = Guid.NewGuid();
    private readonly Guid _volunteer = Guid.NewGuid();

    public OrganizationServiceAuthorizationTests()
    {
        _service = new OrganizationService(_db, _cache, new OrganizationAccessGuard(_db));
        _db.Organizations.Add(_org);
        AddMember(_db, _org.Id, _admin, MembershipRole.Admin);
        AddMember(_db, _org.Id, _coordinator, MembershipRole.Coordinator);
        AddMember(_db, _org.Id, _volunteer, MembershipRole.Volunteer);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    // --- memberships (CRITICAL: self-promotion to Admin) -----------------------

    [Fact]
    public async Task AddMembership_ReturnsNotFound_WhenOutsiderAddsThemselvesAsAdmin()
    {
        var outsider = Guid.NewGuid();

        var result = await _service.AddMembershipAsync(
            _org.Id, new AddMembershipRequest(outsider, MembershipRole.Admin), outsider, callerIsPlatformAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        _db.OrganizationMemberships.Should().NotContain(m => m.UserId == outsider);
    }

    [Fact]
    public async Task AddMembership_ReturnsForbidden_WhenVolunteerPromotesThemselves()
    {
        var result = await _service.AddMembershipAsync(
            _org.Id, new AddMembershipRequest(Guid.NewGuid(), MembershipRole.Admin), _volunteer, callerIsPlatformAdmin: false);

        result.Error!.Code.Should().Be("CAPABILITY_MISSING");
    }

    [Fact]
    public async Task AddMembership_ReturnsForbidden_ForCoordinator()
    {
        var result = await _service.AddMembershipAsync(
            _org.Id, new AddMembershipRequest(Guid.NewGuid(), MembershipRole.Volunteer), _coordinator, callerIsPlatformAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task AddMembership_Succeeds_ForOrgAdmin()
    {
        var newUser = Guid.NewGuid();

        var result = await _service.AddMembershipAsync(
            _org.Id, new AddMembershipRequest(newUser, MembershipRole.Volunteer), _admin, callerIsPlatformAdmin: false);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task AddMembership_Succeeds_ForPlatformAdminBootstrap()
    {
        var firstAdmin = Guid.NewGuid();

        var result = await _service.AddMembershipAsync(
            _org.Id, new AddMembershipRequest(firstAdmin, MembershipRole.Admin), Guid.NewGuid(), callerIsPlatformAdmin: true);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateOrganization_ReturnsForbidden_ForNonPlatformAdmin()
    {
        var result = await _service.CreateOrganizationAsync(
            new CreateOrganizationRequest("New Org", OrganizationType.Association), Guid.NewGuid(), callerIsPlatformAdmin: false);

        result.Error!.Code.Should().Be("CAPABILITY_MISSING");
    }

    [Fact]
    public async Task GetMemberships_ReturnsForbidden_ForVolunteer_AndSucceedsForStaff()
    {
        (await _service.GetMembershipsAsync(_org.Id, _volunteer)).Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _service.GetMembershipsAsync(_org.Id, Guid.NewGuid())).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await _service.GetMembershipsAsync(_org.Id, _coordinator)).Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task SetPolicy_RejectsInvalidJson_AndRequiresAdmin()
    {
        (await _service.SetPolicyAsync(_org.Id, "k", "{}", _coordinator)).Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _service.SetPolicyAsync(_org.Id, "k", "not json", _admin)).Error!.Kind.Should().Be(ErrorKind.Validation);
        (await _service.SetPolicyAsync(_org.Id, "k", """{"a":1}""", _admin)).IsSuccess.Should().BeTrue();
    }

    // --- intake forms ---------------------------------------------------------

    [Fact]
    public async Task CreateIntakeForm_ReturnsNotFound_ForNonMember_AndForbidden_ForVolunteer()
    {
        var request = new CreateIntakeFormRequest(FormType.Volunteer, "Form");

        (await _service.CreateIntakeFormAsync(_org.Id, request, Guid.NewGuid())).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await _service.CreateIntakeFormAsync(_org.Id, request, _volunteer)).Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _service.CreateIntakeFormAsync(_org.Id, request, _coordinator)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AddSection_ReturnsNotFound_WhenFormBelongsToAnotherOrganization()
    {
        // Arrange: staff of _org targets a form id of a foreign org.
        var foreignForm = OrganizationIntakeForm.Create(Guid.NewGuid(), FormType.Volunteer, "Foreign", null, null);
        _db.OrganizationIntakeForms.Add(foreignForm);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.AddSectionAsync(
            _org.Id, foreignForm.Id, new CreateIntakeFormSectionRequest("Injected"), _coordinator);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        _db.IntakeFormSections.Should().BeEmpty();
    }

    [Fact]
    public async Task AddField_ReturnsNotFound_WhenSectionBelongsToAnotherOrganizationsForm()
    {
        var foreignForm = OrganizationIntakeForm.Create(Guid.NewGuid(), FormType.Volunteer, "Foreign", null, null);
        var foreignSection = IntakeFormSection.Create(foreignForm.Id, "S", null, 1, null);
        var ownForm = OrganizationIntakeForm.Create(_org.Id, FormType.Volunteer, "Own", null, null);
        _db.OrganizationIntakeForms.AddRange(foreignForm, ownForm);
        _db.IntakeFormSections.Add(foreignSection);
        await _db.SaveChangesAsync();

        var result = await _service.AddFieldAsync(
            _org.Id, ownForm.Id, foreignSection.Id,
            new CreateIntakeFormFieldRequest("k", "l", FieldType.Text), _coordinator);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task ActivateStandardTemplate_ReturnsForbidden_ForVolunteer()
    {
        var result = await _service.ActivateStandardTemplateAsync(
            _org.Id, new ActivateStandardTemplateRequest(FormType.Volunteer), _volunteer);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Submissions_AreStaffOnly_AndStaffCannotDecideTheirOwn()
    {
        // Arrange
        var form = OrganizationIntakeForm.Create(_org.Id, FormType.Volunteer, "Form", null, null);
        _db.OrganizationIntakeForms.Add(form);
        var ownSubmission = IntakeFormSubmission.Create(form.Id, _org.Id, _coordinator, "{}", true, true, false, _coordinator);
        var applicantSubmission = IntakeFormSubmission.Create(form.Id, _org.Id, _volunteer, "{}", true, true, false, _volunteer);
        _db.IntakeFormSubmissions.AddRange(ownSubmission, applicantSubmission);
        await _db.SaveChangesAsync();
        var approve = new DecideIntakeFormSubmissionRequest(Approve: true);

        // Act + Assert
        (await _service.GetSubmissionsAsync(form.Id, _org.Id, _volunteer)).Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _service.GetSubmissionsAsync(form.Id, _org.Id, Guid.NewGuid())).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await _service.GetSubmissionsAsync(form.Id, _org.Id, _coordinator)).Value.Should().HaveCount(2);

        (await _service.DecideSubmissionAsync(form.Id, _org.Id, applicantSubmission.Id, approve, _volunteer))
            .Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _service.DecideSubmissionAsync(form.Id, _org.Id, ownSubmission.Id, approve, _coordinator))
            .Error!.Code.Should().Be("SELF_DECISION_FORBIDDEN");

        var decided = await _service.DecideSubmissionAsync(form.Id, _org.Id, applicantSubmission.Id, approve, _coordinator);
        decided.Value!.Status.Should().Be(SubmissionStatus.Approved);

        (await _service.DecideSubmissionAsync(form.Id, _org.Id, applicantSubmission.Id, approve, _admin))
            .Error!.Code.Should().Be("INVALID_STATE_TRANSITION", "a decided submission is not decided twice");
    }
}
