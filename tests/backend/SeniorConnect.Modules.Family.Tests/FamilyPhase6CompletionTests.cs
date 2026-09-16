using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Notifications.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class FamilyPhase6CompletionTests
{
    private static TestFamilyDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TestFamilyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestFamilyDbContext(options);
    }

    private sealed class MockSeniorAccountProvisioner : ISeniorAccountProvisioner
    {
        public Guid GeneratedSeniorId { get; set; } = Guid.NewGuid();
        public bool WasCalled { get; private set; }
        public string? ProvisionedDisplayName { get; private set; }
        public string? ProvisionedPhone { get; private set; }

        public Task<Result<Guid>> ProvisionSeniorUserAsync(string displayName, string? phone, Guid createdByUserId, CancellationToken ct = default)
        {
            WasCalled = true;
            ProvisionedDisplayName = displayName;
            ProvisionedPhone = phone;
            return Task.FromResult(Result<Guid>.Success(GeneratedSeniorId));
        }
    }

    private sealed class MockSupportProfileProvisioner : ISupportProfileProvisioner
    {
        public bool WasCalled { get; private set; }
        public Guid? ProvisionedUserId { get; private set; }
        public string? ProvisionedPostalCode { get; private set; }
        public string? ProvisionedCity { get; private set; }

        public Task<Result> ProvisionSupportProfileAsync(Guid userId, string? postalCode, string? city, Guid? createdByUserId, CancellationToken ct = default)
        {
            WasCalled = true;
            ProvisionedUserId = userId;
            ProvisionedPostalCode = postalCode;
            ProvisionedCity = city;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class MockUserSessionIssuer : IUserSessionIssuer
    {
        public bool WasCalled { get; private set; }
        public Guid? IssuedUserId { get; private set; }

        public Task<Result<UserSessionDto>> IssueSessionAsync(Guid userId, string? deviceLabel = null, CancellationToken ct = default)
        {
            WasCalled = true;
            IssuedUserId = userId;
            return Task.FromResult(Result<UserSessionDto>.Success(new UserSessionDto("test.jwt.access", "test.refresh.token", 900, userId, "Maria Huber")));
        }
    }

    private sealed class MockUserContactReader : IUserContactReader
    {
        private readonly Dictionary<Guid, UserContact> _contacts = [];

        public void Add(Guid userId, string displayName, string? phone) => _contacts[userId] = new UserContact(displayName, phone);

        public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default)
        {
            _contacts.TryGetValue(userId, out var contact);
            return Task.FromResult(contact);
        }
    }

    private sealed class MockNotificationDispatcher : INotificationDispatcher
    {
        public List<NotificationDispatchCommand> DispatchedCommands { get; } = [];
        public List<(string Phone, string Message)> DispatchedSms { get; } = [];

        public Task<Result> DispatchAsync(NotificationDispatchCommand command, CancellationToken ct = default)
        {
            DispatchedCommands.Add(command);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> DispatchDirectSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
        {
            DispatchedSms.Add((phoneNumber, message));
            return Task.FromResult(Result.Success());
        }
    }

    [Fact]
    public async Task P6_03_CreateSeniorWithZugangskarte_ProvisionsRealUserAndSupportProfile()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var mockAccount = new MockSeniorAccountProvisioner();
        var mockProfile = new MockSupportProfileProvisioner();
        var service = new FamilyService(db, mockAccount, mockProfile);
        var caregiverId = Guid.NewGuid();

        var request = new CreateSeniorWithZugangskarteRequest(
            DisplayName: "Maria Huber",
            PhoneNumber: "+436641234567",
            PostalCode: "6020",
            City: "Innsbruck",
            RelationshipType: RelationshipType.Child);

        // Act
        var result = await service.CreateSeniorWithZugangskarteAsync(caregiverId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockAccount.WasCalled.Should().BeTrue();
        mockAccount.ProvisionedDisplayName.Should().Be("Maria Huber");
        mockAccount.ProvisionedPhone.Should().Be("+436641234567");

        mockProfile.WasCalled.Should().BeTrue();
        mockProfile.ProvisionedUserId.Should().Be(mockAccount.GeneratedSeniorId);
        mockProfile.ProvisionedPostalCode.Should().Be("6020");
        mockProfile.ProvisionedCity.Should().Be("Innsbruck");

        result.Value!.SeniorUserId.Should().Be(mockAccount.GeneratedSeniorId);
        result.Value.CreatedByCaregiverUserId.Should().Be(caregiverId);
    }

    [Fact]
    public async Task P6_04_ClaimZugangskarte_AnonymousClaim_BindsUserAndIssuesSession()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var mockSession = new MockUserSessionIssuer();
        var service = new FamilyService(db, userSessionIssuer: mockSession);
        var seniorUserId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var karte = Zugangskarte.Generate(seniorUserId, caregiverId, TimeSpan.FromDays(7));
        db.Zugangskarten.Add(karte);
        await db.SaveChangesAsync();

        // Act: anonymous senior opens app and claims pairing code
        var claimResult = await service.ClaimZugangskarteAsync(null, new ClaimZugangskarteRequest(karte.PairingCode));

        // Assert
        claimResult.IsSuccess.Should().BeTrue();
        claimResult.Value!.Zugangskarte.IsClaimed.Should().BeTrue();
        claimResult.Value.Session.Should().NotBeNull();
        claimResult.Value.Session!.AccessToken.Should().Be("test.jwt.access");
        claimResult.Value.Session.UserId.Should().Be(seniorUserId);

        mockSession.WasCalled.Should().BeTrue();
        mockSession.IssuedUserId.Should().Be(seniorUserId);

        var dbKarte = await db.Zugangskarten.FirstAsync(z => z.Id == karte.Id);
        dbKarte.IsClaimed.Should().BeTrue();
        dbKarte.ClaimedByUserId.Should().Be(seniorUserId);
    }

    [Fact]
    public async Task P6_04_ClaimZugangskarte_BruteForceProtection_LocksAfterFiveFailures()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorUserId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var karte = Zugangskarte.Generate(seniorUserId, caregiverId, TimeSpan.FromDays(7));
        db.Zugangskarten.Add(karte);
        await db.SaveChangesAsync();

        // Act: 5 failed QR token attempts against the same card
        for (int i = 0; i < 5; i++)
        {
            var failResult = await service.ClaimZugangskarteAsync(seniorUserId, new ClaimZugangskarteRequest(karte.PairingCode, QrToken: "wrong-token"));
            failResult.IsSuccess.Should().BeFalse();
        }

        // 6th attempt: even with correct QR token or no QR token, it must be locked
        var lockedResult = await service.ClaimZugangskarteAsync(seniorUserId, new ClaimZugangskarteRequest(karte.PairingCode));

        // Assert
        lockedResult.IsSuccess.Should().BeFalse();
        lockedResult.Error.Code.Should().Be("ZUGANGSKARTE_EXHAUSTED");
    }

    [Fact]
    public async Task BruteForceProtection_InvitationCode_LocksAfterFiveFailures()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorUserId = Guid.NewGuid();
        var invCode = "998877";

        var rel = FamilyRelationship.CreateInvitation(seniorUserId, RelationshipType.Neighbor, invCode, DateTimeOffset.UtcNow.AddDays(7));
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        // 5 failed attempts where senior tries to accept their own invite (self-caregiver rejected)
        for (int i = 0; i < 5; i++)
        {
            var failResult = await service.AcceptInvitationAsync(seniorUserId, new AcceptInvitationRequest(invCode));
            failResult.IsSuccess.Should().BeFalse();
        }

        // 6th attempt with a legitimate caregiver
        var caregiverId = Guid.NewGuid();
        var lockedResult = await service.AcceptInvitationAsync(caregiverId, new AcceptInvitationRequest(invCode));

        // Assert
        lockedResult.IsSuccess.Should().BeFalse();
        lockedResult.Error.Code.Should().Be("INVITATION_EXHAUSTED");
    }

    [Fact]
    public async Task P6_06_TriggerSafetyAlert_DispatchesNotificationsAndDirectSms()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var mockDispatcher = new MockNotificationDispatcher();
        var service = new FamilyService(db, notificationDispatcher: mockDispatcher);
        var seniorId = Guid.NewGuid();
        var caregiverWithPerm = Guid.NewGuid();
        var caregiverWithoutPerm = Guid.NewGuid();

        // Caregiver 1 has ReceiveSafetyAlerts
        var rel1 = FamilyRelationship.CreateActive(seniorId, caregiverWithPerm, RelationshipType.Child);
        rel1.UpdatePermission(PermissionType.ReceiveSafetyAlerts, true);

        // Caregiver 2 has ReceiveSafetyAlerts revoked
        var rel2 = FamilyRelationship.CreateActive(seniorId, caregiverWithoutPerm, RelationshipType.Neighbor);
        rel2.UpdatePermission(PermissionType.ReceiveSafetyAlerts, false);

        db.FamilyRelationships.AddRange(rel1, rel2);

        // Trusted Contact with notify enabled
        var contact1 = TrustedContact.Create(seniorId, "Dr. Müller", "+43699111222", "Hausarzt", notifyOnSafetyAlert: true).Value!;
        // Trusted Contact with notify disabled
        var contact2 = TrustedContact.Create(seniorId, "Nachbar Franz", "+43699333444", "Nachbar", notifyOnSafetyAlert: false).Value!;

        db.TrustedContacts.AddRange(contact1, contact2);
        await db.SaveChangesAsync();

        // Act: senior triggers safety alert
        var alertResult = await service.TriggerSafetyAlertAsync(seniorId, new TriggerSafetyAlertRequest(
            SeniorUserId: seniorId,
            Category: SafetyAlertCategory.UnusualInactivity,
            Details: "Ungewöhnliche Inaktivität registriert"));

        // Assert
        alertResult.IsSuccess.Should().BeTrue();

        // Verify caregiver notifications: only caregiverWithPerm should receive
        mockDispatcher.DispatchedCommands.Should().HaveCount(1);
        mockDispatcher.DispatchedCommands[0].RecipientUserId.Should().Be(caregiverWithPerm);
        mockDispatcher.DispatchedCommands[0].Category.Should().Be("FamilyWelfare");
        mockDispatcher.DispatchedCommands[0].Priority.Should().Be("CriticalSafety");

        // Verify direct SMS: only contact1 should receive SMS
        mockDispatcher.DispatchedSms.Should().HaveCount(1);
        mockDispatcher.DispatchedSms[0].Phone.Should().Be("+43699111222");
        mockDispatcher.DispatchedSms[0].Message.Should().Contain("UnusualInactivity");
    }

    [Fact]
    public async Task GetSeniorsForCaregiver_PopulatesDisplayNameAndPhoneFromUserContactReader()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var mockContactReader = new MockUserContactReader();
        var service = new FamilyService(db, userContactReader: mockContactReader);

        var caregiverId = Guid.NewGuid();
        var seniorId = Guid.NewGuid();

        mockContactReader.Add(seniorId, "Oma Gertrude", "+43664987654");

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetSeniorsForCaregiverAsync(caregiverId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].SeniorDisplayName.Should().Be("Oma Gertrude");
        result.Value[0].SeniorPhoneNumber.Should().Be("+43664987654");
    }

    private sealed class TestFamilyDbContext : DbContext, IFamilyDbContext
    {
        public TestFamilyDbContext(DbContextOptions<TestFamilyDbContext> options) : base(options) { }

        public DbSet<FamilyRelationship> FamilyRelationships => Set<FamilyRelationship>();
        public DbSet<FamilyPermission> FamilyPermissions => Set<FamilyPermission>();
        public DbSet<SeniorAccessLog> SeniorAccessLogs => Set<SeniorAccessLog>();
        public DbSet<TrustedContact> TrustedContacts => Set<TrustedContact>();
        public DbSet<SafetyAlert> SafetyAlerts => Set<SafetyAlert>();
        public DbSet<Zugangskarte> Zugangskarten => Set<Zugangskarte>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FamilyRelationship>(b =>
            {
                b.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.FamilyRelationshipId);
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
