using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using SeniorConnect.Modules.Identity.Contracts;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

/// <summary>
/// Regression tests for the 2026-09 Family &amp; Delegation audit:
/// P6-04 Zugangskarte brute force, P6-01 invitation defaults / self-accept /
/// duplicates / LegalGuardian, P6-02 permission changes, P6-03 self-grant on
/// provisioning, and safety-alert delivery honesty when SMS is unavailable.
/// </summary>
public sealed class FamilyDelegationSecurityTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    private sealed class RecordingSessionIssuer : IUserSessionIssuer
    {
        public bool WasCalled { get; private set; }

        public Task<Result<UserSessionDto>> IssueSessionAsync(Guid userId, string? deviceLabel = null, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(Result<UserSessionDto>.Success(new UserSessionDto("a", "r", 900, userId, "Senior")));
        }
    }

    private static async Task<Zugangskarte> AddKarteAsync(SeniorConnectDbContext db)
    {
        var karte = Zugangskarte.Generate(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromDays(7));
        db.Zugangskarten.Add(karte);
        await db.SaveChangesAsync();
        return karte;
    }

    private static string WrongCodeFor(Zugangskarte karte) => karte.PairingCode == "123456" ? "654321" : "123456";

    private static async Task<FamilyRelationship> AddActiveAsync(
        SeniorConnectDbContext db, Guid seniorId, Guid caregiverId, RelationshipType type = RelationshipType.Child, params PermissionType[] grants)
    {
        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, type);
        foreach (var g in grants)
        {
            rel.UpdatePermission(g, true);
        }

        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();
        return rel;
    }

    // ---------------------------------------------------------------- P6-04

    [Fact]
    public void Generate_QrToken_Is128BitHex()
    {
        // Act
        var karte = Zugangskarte.Generate(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromDays(7));

        // Assert
        karte.QrToken.Should().MatchRegex("^[0-9a-f]{32}$");
        karte.QrPayload.Should().Contain(karte.QrToken).And.Contain(karte.PairingCode);
    }

    [Fact]
    public async Task Claim_WithCodeButWithoutQrToken_IsRejected_AndIssuesNoSession()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var sessions = new RecordingSessionIssuer();
        var service = new FamilyService(db, userSessionIssuer: sessions);
        var karte = await AddKarteAsync(db);

        // Act
        var result = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(karte.PairingCode, null));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("QR_TOKEN_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Validation);
        sessions.WasCalled.Should().BeFalse();
        (await db.Zugangskarten.FirstAsync(z => z.Id == karte.Id)).IsClaimed.Should().BeFalse();
    }

    [Fact]
    public async Task Claim_WithWrongCode_IncrementsFailedAttemptsOnThatCard()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var karte = await AddKarteAsync(db);

        // Act
        var result = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(WrongCodeFor(karte), karte.QrToken));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("INVALID_PAIRING_CODE");
        result.Error.Extensions!["remainingAttempts"].Should().Be(4);
        (await db.Zugangskarten.AsNoTracking().FirstAsync(z => z.Id == karte.Id)).FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Claim_AfterFiveWrongCodes_LocksCard_RejectsCorrectCode_AndLogsLock()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var sessions = new RecordingSessionIssuer();
        var service = new FamilyService(db, userSessionIssuer: sessions);
        var karte = await AddKarteAsync(db);

        // Act
        Result<ClaimZugangskarteResponse> last = default;
        for (var i = 0; i < 5; i++)
        {
            last = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(WrongCodeFor(karte), karte.QrToken));
        }

        var correctAfterLock = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(karte.PairingCode, karte.QrToken));

        // Assert
        last.Error!.Code.Should().Be("ZUGANGSKARTE_EXHAUSTED");
        correctAfterLock.IsFailure.Should().BeTrue();
        correctAfterLock.Error!.Code.Should().Be("ZUGANGSKARTE_EXHAUSTED");
        sessions.WasCalled.Should().BeFalse();
        (await db.SeniorAccessLogs.CountAsync(l => l.SeniorUserId == karte.SeniorUserId && l.Action == "ZUGANGSKARTE_LOCKED")).Should().Be(1);
    }

    [Fact]
    public async Task Claim_WithTokenAndCorrectCode_Succeeds_AndSecondClaimIsRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var karte = await AddKarteAsync(db);

        // Act
        var first = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(karte.PairingCode, karte.QrToken));
        var second = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(karte.PairingCode, karte.QrToken));

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.Error!.Code.Should().Be("ALREADY_CLAIMED");
    }

    // ---------------------------------------------------------------- P6-03

    [Fact]
    public async Task CreateSeniorWithZugangskarte_AsLegalGuardian_IsRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);

        // Act
        var result = await service.CreateSeniorWithZugangskarteAsync(
            Guid.NewGuid(),
            new CreateSeniorWithZugangskarteRequest("Maria", null, "6020", "Innsbruck", RelationshipType.LegalGuardian));

        // Assert
        result.Error!.Code.Should().Be("LEGAL_GUARDIAN_REQUIRES_ORGANIZATION");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
        (await db.FamilyRelationships.CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------- P6-01

    [Fact]
    public async Task Invite_BySenior_CreatesHighEntropyCode_GrantingNothing_AndIsAudited()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();

        // Act
        var result = await service.InviteCaregiverAsync(seniorId, new InviteCaregiverRequest(seniorId, RelationshipType.Child));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.InvitationCode.Should().MatchRegex("^[ABCDEFGHJKMNPQRSTVWXYZ2-9]{10}$");
        result.Value.Permissions.Should().NotBeEmpty().And.OnlyContain(p => !p.IsGranted);
        (await db.SeniorAccessLogs.AnyAsync(l => l.Action == "INVITATION_CREATED" && l.AccessedByUserId == seniorId)).Should().BeTrue();
    }

    [Fact]
    public async Task Invite_WithLegalGuardianType_IsRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();

        // Act
        var result = await service.InviteCaregiverAsync(seniorId, new InviteCaregiverRequest(seniorId, RelationshipType.LegalGuardian));

        // Assert
        result.Error!.Code.Should().Be("LEGAL_GUARDIAN_REQUIRES_ORGANIZATION");
    }

    [Fact]
    public async Task Invite_ByStranger_IsForbidden()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);

        // Act
        var result = await service.InviteCaregiverAsync(Guid.NewGuid(), new InviteCaregiverRequest(Guid.NewGuid(), RelationshipType.Child));

        // Assert
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Invite_ByLegacyLegalGuardianWithoutManageSettings_IsForbidden()
    {
        // Arrange: a relationship *typed* LegalGuardian must not confer authority
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        await AddActiveAsync(db, seniorId, guardianId, RelationshipType.LegalGuardian);

        // Act
        var result = await service.InviteCaregiverAsync(guardianId, new InviteCaregiverRequest(seniorId, RelationshipType.Child));

        // Assert
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Accept_ByTheCaregiverWhoCreatedTheInvitation_IsRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        await AddActiveAsync(db, seniorId, caregiverId, RelationshipType.Child, PermissionType.ManageSettings);
        var invite = await service.InviteCaregiverAsync(caregiverId, new InviteCaregiverRequest(seniorId, RelationshipType.Sibling));

        // Act
        var result = await service.AcceptInvitationAsync(caregiverId, new AcceptInvitationRequest(invite.Value!.InvitationCode!));

        // Assert
        result.Error!.Code.Should().Be("INVITER_CANNOT_ACCEPT");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
        (await db.FamilyRelationships.CountAsync(r => r.CaregiverUserId == caregiverId)).Should().Be(1);
    }

    [Fact]
    public async Task Accept_ByInviterAfterTheirOwnRelationshipWasRevoked_IsStillRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var rel = await AddActiveAsync(db, seniorId, caregiverId, RelationshipType.Child, PermissionType.ManageSettings);
        var invite = await service.InviteCaregiverAsync(caregiverId, new InviteCaregiverRequest(seniorId, RelationshipType.Sibling));
        (await service.RevokeRelationshipAsync(rel.Id, seniorId)).IsSuccess.Should().BeTrue();

        // Act
        var result = await service.AcceptInvitationAsync(caregiverId, new AcceptInvitationRequest(invite.Value!.InvitationCode!));

        // Assert
        result.Error!.Code.Should().Be("INVITER_CANNOT_ACCEPT");
    }

    [Fact]
    public async Task Accept_WhenAlreadyActivelyLinked_IsRejectedAsDuplicate()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        await AddActiveAsync(db, seniorId, caregiverId);
        var invite = await service.InviteCaregiverAsync(seniorId, new InviteCaregiverRequest(seniorId, RelationshipType.Child));

        // Act
        var result = await service.AcceptInvitationAsync(caregiverId, new AcceptInvitationRequest(invite.Value!.InvitationCode!));

        // Assert
        result.Error!.Code.Should().Be("RELATIONSHIP_ALREADY_EXISTS");
        (await db.FamilyRelationships.CountAsync(r => r.CaregiverUserId == caregiverId)).Should().Be(1);
    }

    [Fact]
    public async Task Accept_WithLowercaseDashedCode_Succeeds_AndGrantsNothing()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var code = (await service.InviteCaregiverAsync(seniorId, new InviteCaregiverRequest(seniorId, RelationshipType.Child))).Value!.InvitationCode!;
        var typed = $"{code[..5].ToLowerInvariant()}-{code[5..].ToLowerInvariant()}";

        // Act
        var result = await service.AcceptInvitationAsync(caregiverId, new AcceptInvitationRequest(typed));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RelationshipStatus.Active);
        result.Value.Permissions.Should().OnlyContain(p => !p.IsGranted);
    }

    // ---------------------------------------------------------------- P6-02

    [Fact]
    public async Task UpdatePermissions_ByCaregiverOnOwnRelationship_IsForbiddenAsSelfGrant()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var rel = await AddActiveAsync(db, seniorId, caregiverId, RelationshipType.LegalGuardian);

        // Act
        var result = await service.UpdatePermissionsAsync(rel.Id, caregiverId, new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool> { [PermissionType.ManageSettings] = true }));

        // Assert
        result.Error!.Code.Should().Be("SELF_GRANT_FORBIDDEN");
        var stored = await db.FamilyRelationships.AsNoTracking().Include(r => r.Permissions).FirstAsync(r => r.Id == rel.Id);
        stored.HasPermission(PermissionType.ManageSettings).Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePermissions_ByAnotherCaregiverTypedLegalGuardian_IsForbidden()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        await AddActiveAsync(db, seniorId, guardianId, RelationshipType.LegalGuardian);
        var target = await AddActiveAsync(db, seniorId, Guid.NewGuid());

        // Act
        var result = await service.UpdatePermissionsAsync(target.Id, guardianId, new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool> { [PermissionType.CreateHelpRequestsOnBehalf] = true }));

        // Assert
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task UpdatePermissions_OnRevokedRelationship_IsRejected()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var rel = await AddActiveAsync(db, seniorId, Guid.NewGuid());
        await service.RevokeRelationshipAsync(rel.Id, seniorId);

        // Act
        var result = await service.UpdatePermissionsAsync(rel.Id, seniorId, new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool> { [PermissionType.ViewActivities] = true }));

        // Assert
        result.Error!.Code.Should().Be("RELATIONSHIP_NOT_ACTIVE");
        (await db.FamilyPermissions.AsNoTracking().AnyAsync(p => p.FamilyRelationshipId == rel.Id && p.IsGranted)).Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePermissions_UnknownRelationship_ReturnsNotFound()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);

        // Act
        var result = await service.UpdatePermissionsAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool> { [PermissionType.ViewActivities] = true }));

        // Assert
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdatePermissions_BySenior_AuditsWhoChangedWhatAndWhy()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var rel = await AddActiveAsync(db, seniorId, Guid.NewGuid());

        // Act
        var result = await service.UpdatePermissionsAsync(rel.Id, seniorId, new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool>
            {
                [PermissionType.CreateHelpRequestsOnBehalf] = true,
                [PermissionType.ViewActivities] = false, // unchanged — not listed as a change
            },
            Reason: "Tochter hilft beim Einkaufen"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var log = await db.SeniorAccessLogs.SingleAsync(l => l.Action == "PERMISSIONS_UPDATED");
        log.AccessedByUserId.Should().Be(seniorId);
        log.ResourceAccessed.Should().Be($"FamilyPermission:{rel.Id}");
        log.PlainLanguageDescription.Should().Contain("CreateHelpRequestsOnBehalf: erteilt")
            .And.NotContain("ViewActivities")
            .And.Contain("Grund: Tochter hilft beim Einkaufen");
    }

    [Fact]
    public void UpdatePermission_OnPendingInvitation_IsRejected()
    {
        // Arrange
        var rel = FamilyRelationship.CreateInvitation(Guid.NewGuid(), RelationshipType.Child, "ABCDEFGHJK", DateTimeOffset.UtcNow.AddDays(7));

        // Act
        var result = rel.UpdatePermission(PermissionType.ManageSettings, true);

        // Assert
        result.Error!.Code.Should().Be("RELATIONSHIP_NOT_ACTIVE");
        rel.Permissions.Should().OnlyContain(p => !p.IsGranted);
    }

    // ------------------------------------------------ Safety alert delivery

    [Fact]
    public async Task TriggerSafetyAlert_WhenSmsProviderUnavailable_SucceedsButDoesNotReportDelivery()
    {
        // Arrange: default dispatcher has no SMS provider
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        db.TrustedContacts.Add(TrustedContact.Create(seniorId, "Dr. Müller", "+43699111222", "Hausarzt", notifyOnSafetyAlert: true).Value!);
        await db.SaveChangesAsync();

        // Act
        var result = await service.TriggerSafetyAlertAsync(seniorId, new TriggerSafetyAlertRequest(
            seniorId, SafetyAlertCategory.MissedCheckIn, "Check-in verpasst"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await db.SeniorAccessLogs.AnyAsync(l => l.Action == "SAFETY_ALERT_NOTIFIED")).Should().BeFalse();
        var failure = await db.SeniorAccessLogs.SingleAsync(l => l.Action == "SAFETY_ALERT_NOTIFICATION_FAILED");
        failure.PlainLanguageDescription.Should().Contain("1 Notfallkontakte");
    }
}
