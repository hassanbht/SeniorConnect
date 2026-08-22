using SeniorConnect.Domain;
using SeniorConnect.Modules.Family.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class FamilyDomainTests
{
    [Fact]
    public void FamilyRelationship_ActiveCreation_SetsDefaultPermissions()
    {
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);

        Assert.Equal(RelationshipStatus.Active, rel.Status);
        Assert.Equal(RelationshipType.Child, rel.RelationshipType);
        Assert.True(rel.HasPermission(PermissionType.ViewActivities));
        Assert.True(rel.HasPermission(PermissionType.ReceiveSafetyAlerts));
        Assert.False(rel.HasPermission(PermissionType.ManageSettings));
    }

    [Fact]
    public void FamilyRelationship_GranularPermissionUpdatesAndRevocation_WorkImmediately()
    {
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Sibling);

        // Grant CreateHelpRequestsOnBehalf
        rel.UpdatePermission(PermissionType.CreateHelpRequestsOnBehalf, true);
        Assert.True(rel.HasPermission(PermissionType.CreateHelpRequestsOnBehalf));

        // Revoke ViewActivities
        rel.UpdatePermission(PermissionType.ViewActivities, false);
        Assert.False(rel.HasPermission(PermissionType.ViewActivities));

        // Revoke entire relationship
        var revokeResult = rel.Revoke(seniorId);
        Assert.True(revokeResult.IsSuccess);
        Assert.Equal(RelationshipStatus.Revoked, rel.Status);
        Assert.False(rel.HasPermission(PermissionType.CreateHelpRequestsOnBehalf));
    }

    [Fact]
    public void FamilyRelationship_InvitationFlow_AcceptsAndAssignsCaregiver()
    {
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var rel = FamilyRelationship.CreateInvitation(seniorId, RelationshipType.Neighbor, "123456", expiresAt);
        Assert.Equal(RelationshipStatus.Invited, rel.Status);
        Assert.Equal("123456", rel.InvitationCode);

        var acceptResult = rel.Accept(caregiverId);
        Assert.True(acceptResult.IsSuccess);
        Assert.Equal(RelationshipStatus.Active, rel.Status);
        Assert.Equal(caregiverId, rel.CaregiverUserId);
        Assert.Null(rel.InvitationCode);
    }

    [Fact]
    public void Zugangskarte_GenerationAndClaiming_WorksCorrectly()
    {
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var karte = Zugangskarte.Generate(seniorId, caregiverId, TimeSpan.FromDays(7));

        Assert.Equal(6, karte.PairingCode.Length);
        Assert.Contains(karte.PairingCode, karte.QrPayload);
        Assert.False(karte.IsClaimed);

        var claimResult = karte.Claim();
        Assert.True(claimResult.IsSuccess);
        Assert.True(karte.IsClaimed);

        // Second claim attempt should fail
        var secondClaim = karte.Claim();
        Assert.False(secondClaim.IsSuccess);
    }

    [Fact]
    public void TrustedContact_CreationAndLifecycle_FunctionsCorrectly()
    {
        var seniorId = Guid.NewGuid();

        var contactResult = TrustedContact.Create(
            seniorId,
            name: "Dr. Müller",
            phoneNumber: "+49 170 1234567",
            relationship: "Hausarzt",
            email: "mueller@praxis.de",
            isPrimaryEmergency: true,
            notifyOnSafetyAlert: true);

        Assert.True(contactResult.IsSuccess);
        var contact = contactResult.Value!;
        Assert.Equal("Dr. Müller", contact.Name);
        Assert.True(contact.IsPrimaryEmergency);
        Assert.False(contact.IsDeleted);

        contact.Delete();
        Assert.True(contact.IsDeleted);
    }

    [Fact]
    public void SafetyAlert_CreationAcknowledgementAndResolution_TracksLifecycle()
    {
        var seniorId = Guid.NewGuid();
        var triggerId = Guid.NewGuid();
        var responderId = Guid.NewGuid();

        var alertResult = SafetyAlert.Create(
            seniorId,
            triggerId,
            SafetyAlertCategory.MissedCheckIn,
            "Senior hat den täglichen Check-in um 10:00 Uhr verpasst.");

        Assert.True(alertResult.IsSuccess);
        var alert = alertResult.Value!;
        Assert.Equal(SafetyAlertStatus.Active, alert.Status);

        var ackResult = alert.Acknowledge(responderId);
        Assert.True(ackResult.IsSuccess);
        Assert.Equal(SafetyAlertStatus.Acknowledged, alert.Status);
        Assert.Equal(responderId, alert.AcknowledgedByUserId);

        var resResult = alert.Resolve(responderId, "Mit Senior telefoniert, alles in bester Ordnung.");
        Assert.True(resResult.IsSuccess);
        Assert.Equal(SafetyAlertStatus.Resolved, alert.Status);
        Assert.Equal("Mit Senior telefoniert, alles in bester Ordnung.", alert.ResolutionNotes);
    }
}
