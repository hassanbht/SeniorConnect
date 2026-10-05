using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

/// <summary>
/// Wave-1 negative authorization tests (BR-SG-02). A safeguarding case may only
/// be assigned to an officer of the case's OWN organization. Without this check
/// an officer of org B could be handed an org-A case and would then see it —
/// subject and reporter included — through VisibleCasesAsync.
/// </summary>
public sealed class SafeguardingCaseAssignmentScopeTests
{
    private sealed class TestSafeguardingDbContext(DbContextOptions<TestSafeguardingDbContext> options)
        : DbContext(options), ISafeguardingDbContext
    {
        public DbSet<SafeguardingCase> SafeguardingCases => Set<SafeguardingCase>();
        public DbSet<SafeguardingCaseNote> SafeguardingCaseNotes => Set<SafeguardingCaseNote>();
        public DbSet<SafeguardingAccessLog> SafeguardingAccessLogs => Set<SafeguardingAccessLog>();
        public DbSet<SafeguardingRestriction> SafeguardingRestrictions => Set<SafeguardingRestriction>();
        public DbSet<OperationalTask> OperationalTasks => Set<OperationalTask>();
    }

    /// <summary>Maps a user to the organizations they officer. Everyone else is a non-officer.</summary>
    private sealed class FakeOrgReader(Dictionary<Guid, Guid[]> officerOrgs) : IOrganizationCoordinatorReader
    {
        public Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(
                officerOrgs.Where(kv => kv.Value.Contains(organizationId)).Select(kv => kv.Key).ToList());

        public Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffOrganizationDto>>([]);

        public Task<IReadOnlyList<Guid>> GetSafeguardingOfficerOrganizationIdsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(officerOrgs.TryGetValue(userId, out var orgs) ? orgs : []);

        public Task<IReadOnlyList<Guid>> GetPlatformSafeguardingOfficerUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OfficerOfA = Guid.NewGuid();
    private static readonly Guid OfficerOfB = Guid.NewGuid();
    private static readonly Guid NonOfficer = Guid.NewGuid();
    private static readonly Guid Reporter = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    private static (SafeguardingService Service, TestSafeguardingDbContext Db) CreateService()
    {
        var db = new TestSafeguardingDbContext(new DbContextOptionsBuilder<TestSafeguardingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var reader = new FakeOrgReader(new Dictionary<Guid, Guid[]>
        {
            [OfficerOfA] = [OrgA],
            [OfficerOfB] = [OrgB],
        });

        return (new SafeguardingService(db, reader), db);
    }

    private static async Task<Guid> SeedCaseOfOrgAAsync(SafeguardingService service)
    {
        var raised = await service.RaiseConcernAsync(
            Reporter,
            new RaiseConcernRequest(Subject, "Welfare check requested by family.", SafeguardingSeverity.High, "vulnerability_welfare", OrgA),
            CancellationToken.None);

        raised.IsSuccess.Should().BeTrue();
        return raised.Value!.Id;
    }

    [Fact]
    public async Task Assigning_to_an_officer_of_the_same_organization_succeeds()
    {
        var (service, _) = CreateService();
        var caseId = await SeedCaseOfOrgAAsync(service);

        var result = await service.AssignCaseAsync(
            caseId, OfficerOfA, new AssignCaseRequest(OfficerOfA), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AssignedOfficerUserId.Should().Be(OfficerOfA);
    }

    [Fact]
    public async Task Assigning_to_an_officer_of_another_organization_is_not_found()
    {
        var (service, _) = CreateService();
        var caseId = await SeedCaseOfOrgAAsync(service);

        // OfficerOfB cannot see an org-A case and must not learn that it exists.
        var result = await service.AssignCaseAsync(
            caseId, OfficerOfA, new AssignCaseRequest(OfficerOfB), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Assigning_to_a_non_officer_is_not_found()
    {
        var (service, _) = CreateService();
        var caseId = await SeedCaseOfOrgAAsync(service);

        var result = await service.AssignCaseAsync(
            caseId, OfficerOfA, new AssignCaseRequest(NonOfficer), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task The_foreign_assignee_gains_no_visibility_of_the_case()
    {
        var (service, _) = CreateService();
        var caseId = await SeedCaseOfOrgAAsync(service);

        await service.AssignCaseAsync(caseId, OfficerOfA, new AssignCaseRequest(OfficerOfB), CancellationToken.None);

        // OfficerOfB may not read it either — neither before nor after the
        // rejected assignment attempt.
        var readBack = await service.GetCasesAsync(OrgA, OfficerOfB, CancellationToken.None);
        readBack.IsSuccess.Should().BeTrue();
        readBack.Value.Should().BeEmpty("the rejected assignment granted no visibility");
    }
}

/// <summary>
/// Negative authorization tests for P4-07 (buddy waiver), P4-11 (safeguarding scope),
/// P4-14 (key custody) and P4-15 (expense confirmation).
/// </summary>
public sealed class TrustSafetyAuthorizationTests
{
    private sealed class TestTrustSafetyDbContext(DbContextOptions<TestTrustSafetyDbContext> options)
        : DbContext(options), ITrustSafetyDbContext
    {
        public DbSet<VolunteerApplication> VolunteerApplications => Set<VolunteerApplication>();
        public DbSet<VolunteerApplicationStep> VolunteerApplicationSteps => Set<VolunteerApplicationStep>();
        public DbSet<BuddyAssignment> BuddyAssignments => Set<BuddyAssignment>();
        public DbSet<KeyCustody> KeyCustodies => Set<KeyCustody>();
        public DbSet<ExpenseRecord> ExpenseRecords => Set<ExpenseRecord>();
        public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    }

    private sealed class TestSafeguardingDbContext(DbContextOptions<TestSafeguardingDbContext> options)
        : DbContext(options), ISafeguardingDbContext
    {
        public DbSet<SafeguardingCase> SafeguardingCases => Set<SafeguardingCase>();
        public DbSet<SafeguardingCaseNote> SafeguardingCaseNotes => Set<SafeguardingCaseNote>();
        public DbSet<SafeguardingAccessLog> SafeguardingAccessLogs => Set<SafeguardingAccessLog>();
        public DbSet<SafeguardingRestriction> SafeguardingRestrictions => Set<SafeguardingRestriction>();
        public DbSet<OperationalTask> OperationalTasks => Set<OperationalTask>();
    }

    /// <summary>Maps userId -> organisations the user actively staffs as Coordinator/Admin.</summary>
    private sealed class FakeOrgReader(Dictionary<Guid, Guid[]> staff) : IOrganizationCoordinatorReader
    {
        public Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(staff.Where(kv => kv.Value.Contains(organizationId)).Select(kv => kv.Key).ToList());

        public Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffOrganizationDto>>(
                staff.TryGetValue(userId, out var orgs) ? orgs.Select(o => new StaffOrganizationDto(o, "Org")).ToList() : []);

        // In these tests the same users also hold the SafeguardingOfficer membership of their org.
        public Task<IReadOnlyList<Guid>> GetSafeguardingOfficerOrganizationIdsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(staff.TryGetValue(userId, out var orgs) ? orgs : []);

        // S-4: no platform officers in these authorization tests (org-less cases
        // become invisible to ordinary organization officers — see Phase5PolicyClosureTests).
        public Task<IReadOnlyList<Guid>> GetPlatformSafeguardingOfficerUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid CoordinatorA = Guid.NewGuid();
    private static readonly Guid CoordinatorB = Guid.NewGuid();
    private static readonly Guid Senior = Guid.NewGuid();
    private static readonly Guid Volunteer = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private static readonly FakeOrgReader OrgReader = new(new()
    {
        [CoordinatorA] = [OrgA],
        [CoordinatorB] = [OrgB],
    });

    private static (TrustSafetyService Service, TestTrustSafetyDbContext Db) CreateTrustService()
    {
        var db = new TestTrustSafetyDbContext(new DbContextOptionsBuilder<TestTrustSafetyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (new TrustSafetyService(db, OrgReader), db);
    }

    private static async Task<(TrustSafetyService Service, TestTrustSafetyDbContext Db)> CreateWithVolunteerInOrgAAsync()
    {
        var (service, db) = CreateTrustService();
        var application = VolunteerApplication.Apply(OrgA, Volunteer, null);
        application.Approve(CoordinatorA);
        db.VolunteerApplications.Add(application);
        await db.SaveChangesAsync();
        return (service, db);
    }

    // --- W2: buddy status is not readable by any authenticated caller -------

    [Fact]
    public async Task Buddy_status_is_not_found_for_an_unrelated_user()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.GetBuddyStatusAsync(Volunteer, Stranger);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BUDDY_STATUS_NOT_FOUND");
    }

    [Fact]
    public async Task Buddy_status_is_not_found_for_a_coordinator_of_another_organization()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        // CoordinatorB staffs OrgB; the volunteer belongs to OrgA.
        var result = await service.GetBuddyStatusAsync(Volunteer, CoordinatorB);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BUDDY_STATUS_NOT_FOUND");
    }

    [Fact]
    public async Task A_volunteer_may_read_their_own_buddy_status()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.GetBuddyStatusAsync(Volunteer, Volunteer);

        result.IsSuccess.Should().BeTrue();
        result.Value!.VolunteerUserId.Should().Be(Volunteer);
    }

    [Fact]
    public async Task A_safeguarding_officer_may_read_the_status_and_the_waiver_reason()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();
        await service.WaiveBuddyAsync(Volunteer, CoordinatorA, false,
            new WaiveBuddyRequest("Works only accompanied, two completed L3 assignments."));

        var result = await service.GetBuddyStatusAsync(Volunteer, CoordinatorA, viewerIsSafeguardingOfficer: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WaiverReason.Should().NotBeNullOrEmpty(
            "an officer needs the justification to make a safety decision");
    }

    [Fact]
    public async Task A_volunteer_does_not_see_the_internal_waiver_reason()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();
        await service.WaiveBuddyAsync(Volunteer, CoordinatorA, false,
            new WaiveBuddyRequest("Internal justification not meant for the volunteer."));

        var result = await service.GetBuddyStatusAsync(Volunteer, Volunteer);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsWaived.Should().BeTrue("the volunteer learns the decision...");
        result.Value!.WaiverReason.Should().BeNull("...but not the internal reasoning behind it");
    }

    [Fact]
    public async Task A_refused_caller_learns_nothing_about_the_volunteer()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.GetBuddyStatusAsync(Volunteer, Stranger);

        // Not 403: a 403 would confirm the volunteer exists.
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- P4-07: buddy waiver --------------------------------------------------

    [Fact]
    public async Task Volunteer_cannot_waive_own_buddy_requirement_even_with_officer_capability()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.WaiveBuddyAsync(Volunteer, Volunteer, actorIsSafeguardingOfficer: true, new WaiveBuddyRequest("I know what I'm doing"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("BUDDY_SELF_WAIVER_FORBIDDEN");
    }

    [Fact]
    public async Task Non_coordinator_cannot_waive_buddy_requirement()
    {
        var (service, db) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.WaiveBuddyAsync(Volunteer, Stranger, actorIsSafeguardingOfficer: false, new WaiveBuddyRequest("Looks fine"));

        result.Error!.Code.Should().Be("BUDDY_WAIVER_COORDINATOR_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
        (await db.BuddyAssignments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Coordinator_of_another_organisation_cannot_waive_buddy_requirement()
    {
        var (service, _) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.WaiveBuddyAsync(Volunteer, CoordinatorB, actorIsSafeguardingOfficer: false, new WaiveBuddyRequest("Looks fine"));

        result.Error!.Code.Should().Be("BUDDY_WAIVER_COORDINATOR_REQUIRED");
    }

    [Fact]
    public async Task Coordinator_of_volunteers_organisation_can_waive_and_is_recorded()
    {
        var (service, db) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.WaiveBuddyAsync(Volunteer, CoordinatorA, actorIsSafeguardingOfficer: false, new WaiveBuddyRequest("Former care nurse, 10 years."));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsWaived.Should().BeTrue();
        var buddy = await db.BuddyAssignments.SingleAsync();
        buddy.WaivedByUserId.Should().Be(CoordinatorA);
    }

    [Fact]
    public async Task Safeguarding_officer_can_waive_buddy_requirement()
    {
        var (service, db) = await CreateWithVolunteerInOrgAAsync();

        var result = await service.WaiveBuddyAsync(Volunteer, Stranger, actorIsSafeguardingOfficer: true, new WaiveBuddyRequest("Reviewed by safeguarding."));

        result.IsSuccess.Should().BeTrue();
        (await db.BuddyAssignments.SingleAsync()).WaivedByUserId.Should().Be(Stranger);
    }

    // --- P4-14: key custody ---------------------------------------------------

    [Fact]
    public async Task Stranger_cannot_record_a_key_handover()
    {
        var (service, _) = CreateTrustService();

        var result = await service.HandoverKeyAsync(Stranger, new HandoverKeyRequest(Senior, Volunteer, "Wohnungstür", OrgA));

        result.Error!.Code.Should().Be("KEY_CUSTODY_PARTY_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Key_handover_and_return_record_the_acting_users()
    {
        var (service, db) = CreateTrustService();

        var created = await service.HandoverKeyAsync(Volunteer, new HandoverKeyRequest(Senior, Volunteer, "Wohnungstür", OrgA));
        created.IsSuccess.Should().BeTrue();

        var returned = await service.ReturnKeyAsync(created.Value!.Id, CoordinatorA, new ReturnKeyRequest());
        returned.IsSuccess.Should().BeTrue();

        var key = await db.KeyCustodies.SingleAsync();
        key.CreatedBy.Should().Be(Volunteer);
        key.UpdatedBy.Should().Be(CoordinatorA);
        key.Status.Should().Be(KeyCustodyStatus.Returned);
    }

    [Fact]
    public async Task Outsider_returning_a_key_gets_not_found()
    {
        var (service, _) = CreateTrustService();
        var created = await service.HandoverKeyAsync(Senior, new HandoverKeyRequest(Senior, Volunteer, "Wohnungstür", OrgA));

        var byStranger = await service.ReturnKeyAsync(created.Value!.Id, Stranger, new ReturnKeyRequest());
        var byOtherOrgCoordinator = await service.ReturnKeyAsync(created.Value.Id, CoordinatorB, new ReturnKeyRequest());

        byStranger.Error!.Kind.Should().Be(ErrorKind.NotFound);
        byOtherOrgCoordinator.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- P4-15: expense confirmation -----------------------------------------

    private static CreateExpenseRequest Expense() => new(Senior, Volunteer, 50m, 32.5m, 17.5m);

    [Fact]
    public async Task Stranger_cannot_record_an_expense()
    {
        var (service, _) = CreateTrustService();

        var result = await service.RecordExpenseAsync(Stranger, Expense());

        result.Error!.Code.Should().Be("EXPENSE_PARTY_REQUIRED");
    }

    [Fact]
    public async Task Submitter_cannot_confirm_own_expense()
    {
        var (service, _) = CreateTrustService();
        var created = await service.RecordExpenseAsync(Volunteer, Expense());

        var result = await service.ConfirmExpenseAsync(created.Value!.Id, Volunteer);

        result.Error!.Code.Should().Be("EXPENSE_COUNTERPARTY_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Stranger_confirming_an_expense_gets_not_found()
    {
        var (service, _) = CreateTrustService();
        var created = await service.RecordExpenseAsync(Volunteer, Expense());

        var result = await service.ConfirmExpenseAsync(created.Value!.Id, Stranger);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Counterparty_can_confirm_and_is_recorded()
    {
        var (service, db) = CreateTrustService();
        var created = await service.RecordExpenseAsync(Volunteer, Expense());

        var result = await service.ConfirmExpenseAsync(created.Value!.Id, Senior);

        result.IsSuccess.Should().BeTrue();
        var expense = await db.ExpenseRecords.SingleAsync();
        expense.Status.Should().Be(ExpenseStatus.Confirmed);
        expense.CreatedBy.Should().Be(Volunteer);
        expense.UpdatedBy.Should().Be(Senior);
    }

    [Fact]
    public async Task Confirming_a_disputed_expense_is_rejected_and_keeps_the_dispute()
    {
        var (service, db) = CreateTrustService();
        var created = await service.RecordExpenseAsync(Volunteer, Expense());
        (await service.DisputeExpenseAsync(created.Value!.Id, Senior, new DisputeExpenseRequest("Wrong change"))).IsSuccess.Should().BeTrue();

        var result = await service.ConfirmExpenseAsync(created.Value.Id, Senior);

        result.Error!.Code.Should().Be("INVALID_STATE_TRANSITION");
        var expense = await db.ExpenseRecords.SingleAsync();
        expense.Status.Should().Be(ExpenseStatus.Disputed);
        expense.DisputeReason.Should().Be("Wrong change");
    }

    // --- P4-11: safeguarding scope --------------------------------------------

    private static async Task<(SafeguardingService Service, Guid CaseA, Guid CaseB, Guid CaseNoOrg)> CreateSafeguardingAsync()
    {
        var db = new TestSafeguardingDbContext(new DbContextOptionsBuilder<TestSafeguardingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var caseA = SafeguardingCase.Raise(Senior, Volunteer, "Concern in org A", organizationId: OrgA).Value!;
        var caseB = SafeguardingCase.Raise(Senior, Volunteer, "Concern in org B", organizationId: OrgB).Value!;
        var caseNoOrg = SafeguardingCase.Raise(Senior, Volunteer, "Neighbourly concern").Value!;
        db.SafeguardingCases.AddRange(caseA, caseB, caseNoOrg);
        await db.SaveChangesAsync();
        return (new SafeguardingService(db, OrgReader), caseA.Id, caseB.Id, caseNoOrg.Id);
    }

    [Fact]
    public async Task Officer_lists_only_own_organisation_cases()
    {
        var (service, caseA, caseB, caseNoOrg) = await CreateSafeguardingAsync();

        var result = await service.GetCasesAsync(null, CoordinatorA);

        // S-4 (approved Option B): org-less cases are PLATFORM_LEVEL and are
        // visible only to the explicit PlatformSafeguardingOfficer role — no
        // longer implicitly to an organization officer (pilot-era behaviour).
        result.Value!.Select(c => c.Id).Should().BeEquivalentTo([caseA]);
        result.Value!.Select(c => c.Id).Should().NotContain(caseB);
        result.Value!.Select(c => c.Id).Should().NotContain(caseNoOrg);
    }

    [Fact]
    public async Task Officer_filtering_by_foreign_organisation_gets_nothing()
    {
        var (service, _, _, _) = await CreateSafeguardingAsync();

        var result = await service.GetCasesAsync(OrgB, CoordinatorA);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Officer_reading_or_updating_foreign_organisation_case_gets_not_found()
    {
        var (service, _, caseB, _) = await CreateSafeguardingAsync();

        (await service.GetCaseByIdAsync(caseB, CoordinatorA)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await service.AddCaseNoteAsync(caseB, CoordinatorA, new AddCaseNoteRequest("note"))).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await service.CloseCaseAsync(caseB, CoordinatorA, new CloseCaseRequest("done"))).Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Officer_can_read_own_organisation_case()
    {
        var (service, caseA, _, _) = await CreateSafeguardingAsync();

        (await service.GetCaseByIdAsync(caseA, CoordinatorA)).IsSuccess.Should().BeTrue();
    }
}
