using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;

namespace SeniorConnect.Modules.Family.Infrastructure;

public sealed class FamilyService : IFamilyService
{
    private readonly IFamilyDbContext _db;

    public FamilyService(IFamilyDbContext db)
    {
        _db = db;
    }

    public async Task<Result<FamilyRelationshipDto>> InviteCaregiverAsync(
        Guid requesterUserId,
        InviteCaregiverRequest request,
        CancellationToken ct = default)
    {
        if (request.SeniorUserId != requesterUserId)
        {
            // Verify if requester is legal guardian or already authorized caregiver
            var isAuthorized = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == request.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && (r.RelationshipType == RelationshipType.LegalGuardian || r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted)), ct);

            if (!isAuthorized)
            {
                return Error.Forbidden("Only the senior or authorized guardian can invite caregivers.");
            }
        }

        var invCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays > 0 ? request.ExpiresInDays : 7);

        var relationship = FamilyRelationship.CreateInvitation(
            request.SeniorUserId,
            request.RelationshipType,
            invCode,
            expiresAt);

        _db.FamilyRelationships.Add(relationship);
        await _db.SaveChangesAsync(ct);

        return MapRelationshipToDto(relationship);
    }

    public async Task<Result<FamilyRelationshipDto>> AcceptInvitationAsync(
        Guid caregiverUserId,
        AcceptInvitationRequest request,
        CancellationToken ct = default)
    {
        var rel = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.InvitationCode == request.InvitationCode.Trim()
                && r.Status == RelationshipStatus.Invited, ct);

        if (rel is null)
        {
            return Error.NotFound("Invitation code");
        }

        var acceptResult = rel.Accept(caregiverUserId);
        if (acceptResult.IsFailure)
        {
            return acceptResult.Error!;
        }

        // Log access in "Wer hat was gesehen?"
        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            rel.SeniorUserId,
            caregiverUserId,
            "Angehöriger",
            "INVITATION_ACCEPTED",
            "FamilyRelationship",
            "Angehöriger ist dem Familienkreis beigetreten."));

        await _db.SaveChangesAsync(ct);
        return MapRelationshipToDto(rel);
    }

    public async Task<Result> RevokeRelationshipAsync(
        Guid relationshipId,
        Guid requesterUserId,
        CancellationToken ct = default)
    {
        var rel = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == relationshipId, ct);

        if (rel is null)
        {
            return Error.NotFound("FamilyRelationship");
        }

        // Senior, caregiver themselves, or legal guardian can revoke
        if (rel.SeniorUserId != requesterUserId && rel.CaregiverUserId != requesterUserId)
        {
            return Error.Forbidden("You are not authorized to revoke this relationship.");
        }

        var revokeResult = rel.Revoke(requesterUserId);
        if (revokeResult.IsFailure)
        {
            return revokeResult.Error!;
        }

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            rel.SeniorUserId,
            requesterUserId,
            "Nutzer",
            "RELATIONSHIP_REVOKED",
            "FamilyRelationship",
            "Die Familienverbindung und alle Zugriffsrechte wurden widerrufen."));

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdatePermissionsAsync(
        Guid relationshipId,
        Guid requesterUserId,
        UpdatePermissionsRequest request,
        CancellationToken ct = default)
    {
        var rel = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == relationshipId, ct);

        if (rel is null)
        {
            return Error.NotFound("FamilyRelationship");
        }

        // Only the senior or legal guardian can change permissions
        if (rel.SeniorUserId != requesterUserId)
        {
            var isGuardian = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == rel.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.RelationshipType == RelationshipType.LegalGuardian, ct);

            if (!isGuardian)
            {
                return Error.Forbidden("Only the senior or legal guardian can modify permissions.");
            }
        }

        foreach (var (permType, isGranted) in request.Permissions)
        {
            rel.UpdatePermission(permType, isGranted);
        }

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            rel.SeniorUserId,
            requesterUserId,
            "Senior / Vormund",
            "PERMISSIONS_UPDATED",
            "FamilyPermission",
            "Berechtigungen für den Angehörigen wurden aktualisiert."));

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<FamilyRelationshipDto>>> GetCaregiversForSeniorAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default)
    {
        // Senior themselves or connected active caregiver can view caregivers
        if (seniorUserId != requesterUserId)
        {
            var isCaregiver = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active, ct);

            if (!isCaregiver)
            {
                return Error.Forbidden("Not authorized to view caregiver roster for this senior.");
            }
        }

        var list = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .Where(r => r.SeniorUserId == seniorUserId && r.Status != RelationshipStatus.Revoked)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return list.Select(MapRelationshipToDto).ToList();
    }

    public async Task<Result<IReadOnlyList<FamilyRelationshipDto>>> GetSeniorsForCaregiverAsync(
        Guid caregiverUserId,
        CancellationToken ct = default)
    {
        var list = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .Where(r => r.CaregiverUserId == caregiverUserId && r.Status == RelationshipStatus.Active)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return list.Select(MapRelationshipToDto).ToList();
    }

    public async Task<Result<ZugangskarteDto>> CreateSeniorWithZugangskarteAsync(
        Guid caregiverUserId,
        CreateSeniorWithZugangskarteRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Error.Validation("Senior display name is required.");

        var seniorId = Guid.NewGuid();
        var zugangskarte = Zugangskarte.Generate(seniorId, caregiverUserId, TimeSpan.FromDays(request.ValidForDays > 0 ? request.ValidForDays : 7));

        // Create initial active relationship between caregiver and senior
        var rel = FamilyRelationship.CreateActive(seniorId, caregiverUserId, request.RelationshipType);
        // Grant all initial default setup permissions to provisioning caregiver
        rel.UpdatePermission(PermissionType.ViewActivities, true);
        rel.UpdatePermission(PermissionType.CreateHelpRequestsOnBehalf, true);
        rel.UpdatePermission(PermissionType.ViewEmergencyContacts, true);
        rel.UpdatePermission(PermissionType.ManageSettings, true);
        rel.UpdatePermission(PermissionType.ReceiveSafetyAlerts, true);

        _db.Zugangskarten.Add(zugangskarte);
        _db.FamilyRelationships.Add(rel);

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            seniorId,
            caregiverUserId,
            "Angehöriger (Ersteller)",
            "ACCOUNT_PROVISIONED",
            "SeniorProfile",
            "Konto für den Senior wurde vorbereitet und Zugangskarte erstellt."));

        await _db.SaveChangesAsync(ct);

        return new ZugangskarteDto(
            zugangskarte.Id,
            zugangskarte.SeniorUserId,
            zugangskarte.CreatedByCaregiverUserId,
            zugangskarte.PairingCode,
            zugangskarte.QrPayload,
            zugangskarte.CreatedAtUtc,
            zugangskarte.ExpiresAtUtc,
            zugangskarte.IsClaimed);
    }

    public async Task<Result<ZugangskarteDto>> ClaimZugangskarteAsync(
        Guid seniorUserId,
        ClaimZugangskarteRequest request,
        CancellationToken ct = default)
    {
        var karte = await _db.Zugangskarten
            .FirstOrDefaultAsync(z => z.PairingCode == request.PairingCode.Trim() && !z.ClaimedAtUtc.HasValue, ct);

        if (karte is null)
        {
            return Error.NotFound("Zugangskarte");
        }

        var claimResult = karte.Claim();
        if (claimResult.IsFailure)
        {
            return claimResult.Error!;
        }

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            karte.SeniorUserId,
            seniorUserId,
            "Senior",
            "ZUGANGSKARTE_CLAIMED",
            "Zugangskarte",
            "Senior hat sich erfolgreich mit der Zugangskarte angemeldet."));

        await _db.SaveChangesAsync(ct);

        return new ZugangskarteDto(
            karte.Id,
            karte.SeniorUserId,
            karte.CreatedByCaregiverUserId,
            karte.PairingCode,
            karte.QrPayload,
            karte.CreatedAtUtc,
            karte.ExpiresAtUtc,
            karte.IsClaimed);
    }

    public async Task<Result<IReadOnlyList<SeniorAccessLogDto>>> GetAccessLogsAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        int days = 30,
        CancellationToken ct = default)
    {
        // Senior themselves or caregiver with ManageSettings permission
        if (seniorUserId != requesterUserId)
        {
            var hasAccess = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted), ct);

            if (!hasAccess)
            {
                return Error.Forbidden("Not authorized to view access logs for this senior.");
            }
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Abs(days));
        var logs = await _db.SeniorAccessLogs
            .Where(l => l.SeniorUserId == seniorUserId && l.TimestampUtc >= cutoff)
            .OrderByDescending(l => l.TimestampUtc)
            .ToListAsync(ct);

        return logs.Select(l => new SeniorAccessLogDto(
            l.Id,
            l.SeniorUserId,
            l.AccessedByUserId,
            l.AccessedByUserName,
            l.Action,
            l.ResourceAccessed,
            l.PlainLanguageDescription,
            l.TimestampUtc)).ToList();
    }

    public async Task<Result<TrustedContactDto>> CreateTrustedContactAsync(
        Guid requesterUserId,
        CreateTrustedContactRequest request,
        CancellationToken ct = default)
    {
        if (request.SeniorUserId != requesterUserId)
        {
            var hasPermission = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == request.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted), ct);

            if (!hasPermission)
            {
                return Error.Forbidden("Not authorized to add trusted contacts for this senior.");
            }
        }

        var contactResult = TrustedContact.Create(
            request.SeniorUserId,
            request.Name,
            request.PhoneNumber,
            request.Relationship,
            request.Email,
            request.IsPrimaryEmergency,
            request.NotifyOnSafetyAlert);

        if (contactResult.IsFailure)
        {
            return contactResult.Error!;
        }

        var contact = contactResult.Value!;
        _db.TrustedContacts.Add(contact);

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            request.SeniorUserId,
            requesterUserId,
            "Nutzer",
            "CONTACT_CREATED",
            "TrustedContact",
            $"Notfallkontakt '{contact.Name}' wurde hinzugefügt."));

        await _db.SaveChangesAsync(ct);
        return MapContactToDto(contact);
    }

    public async Task<Result<IReadOnlyList<TrustedContactDto>>> GetTrustedContactsAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default)
    {
        if (seniorUserId != requesterUserId)
        {
            var hasPermission = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ViewEmergencyContacts && p.IsGranted), ct);

            if (!hasPermission)
            {
                return Error.Forbidden("Not authorized to view emergency contacts for this senior.");
            }
        }

        var contacts = await _db.TrustedContacts
            .Where(c => c.SeniorUserId == seniorUserId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimaryEmergency)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return contacts.Select(MapContactToDto).ToList();
    }

    public async Task<Result<TrustedContactDto>> UpdateTrustedContactAsync(
        Guid id,
        Guid requesterUserId,
        UpdateTrustedContactRequest request,
        CancellationToken ct = default)
    {
        var contact = await _db.TrustedContacts
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (contact is null)
        {
            return Error.NotFound("TrustedContact");
        }

        if (contact.SeniorUserId != requesterUserId)
        {
            var hasPermission = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == contact.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted), ct);

            if (!hasPermission)
            {
                return Error.Forbidden("Not authorized to update trusted contacts for this senior.");
            }
        }

        contact.Update(
            request.Name,
            request.PhoneNumber,
            request.Relationship,
            request.Email,
            request.IsPrimaryEmergency,
            request.NotifyOnSafetyAlert);

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            contact.SeniorUserId,
            requesterUserId,
            "Nutzer",
            "CONTACT_UPDATED",
            "TrustedContact",
            $"Notfallkontakt '{contact.Name}' wurde aktualisiert."));

        await _db.SaveChangesAsync(ct);
        return MapContactToDto(contact);
    }

    public async Task<Result> DeleteTrustedContactAsync(
        Guid id,
        Guid requesterUserId,
        CancellationToken ct = default)
    {
        var contact = await _db.TrustedContacts
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (contact is null)
        {
            return Error.NotFound("TrustedContact");
        }

        if (contact.SeniorUserId != requesterUserId)
        {
            var hasPermission = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == contact.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted), ct);

            if (!hasPermission)
            {
                return Error.Forbidden("Not authorized to delete trusted contacts for this senior.");
            }
        }

        contact.Delete();

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            contact.SeniorUserId,
            requesterUserId,
            "Nutzer",
            "CONTACT_DELETED",
            "TrustedContact",
            $"Notfallkontakt '{contact.Name}' wurde entfernt."));

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<SafetyAlertDto>> TriggerSafetyAlertAsync(
        Guid requesterUserId,
        TriggerSafetyAlertRequest request,
        CancellationToken ct = default)
    {
        // Senior themselves or active caregiver can trigger a safety alert
        if (request.SeniorUserId != requesterUserId)
        {
            var hasAccess = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == request.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active, ct);

            if (!hasAccess)
            {
                return Error.Forbidden("Not authorized to trigger safety alerts for this senior.");
            }
        }

        var alertResult = SafetyAlert.Create(
            request.SeniorUserId,
            requesterUserId,
            request.Category,
            request.Details);

        if (alertResult.IsFailure)
        {
            return alertResult.Error!;
        }

        var alert = alertResult.Value!;
        _db.SafetyAlerts.Add(alert);

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            request.SeniorUserId,
            requesterUserId,
            "System / Nutzer",
            "SAFETY_ALERT_TRIGGERED",
            "SafetyAlert",
            $"Sicherheitsbenachrichtigung ({request.Category}) wurde ausgelöst."));

        await _db.SaveChangesAsync(ct);
        return MapAlertToDto(alert);
    }

    public async Task<Result<SafetyAlertDto>> AcknowledgeSafetyAlertAsync(
        Guid alertId,
        Guid caregiverUserId,
        CancellationToken ct = default)
    {
        var alert = await _db.SafetyAlerts.FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert is null) return Error.NotFound("SafetyAlert");

        var hasPermission = await _db.FamilyRelationships
            .AnyAsync(r => r.SeniorUserId == alert.SeniorUserId
                && r.CaregiverUserId == caregiverUserId
                && r.Status == RelationshipStatus.Active
                && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted), ct);

        if (!hasPermission && alert.SeniorUserId != caregiverUserId)
        {
            return Error.Forbidden("Not authorized to acknowledge this alert.");
        }

        var ackResult = alert.Acknowledge(caregiverUserId);
        if (ackResult.IsFailure) return ackResult.Error!;

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            alert.SeniorUserId,
            caregiverUserId,
            "Angehöriger",
            "SAFETY_ALERT_ACKNOWLEDGED",
            "SafetyAlert",
            "Sicherheitsbenachrichtigung wurde zur Kenntnis genommen."));

        await _db.SaveChangesAsync(ct);
        return MapAlertToDto(alert);
    }

    public async Task<Result<SafetyAlertDto>> ResolveSafetyAlertAsync(
        Guid alertId,
        Guid caregiverUserId,
        ResolveSafetyAlertRequest request,
        CancellationToken ct = default)
    {
        var alert = await _db.SafetyAlerts.FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert is null) return Error.NotFound("SafetyAlert");

        var hasPermission = await _db.FamilyRelationships
            .AnyAsync(r => r.SeniorUserId == alert.SeniorUserId
                && r.CaregiverUserId == caregiverUserId
                && r.Status == RelationshipStatus.Active
                && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted), ct);

        if (!hasPermission && alert.SeniorUserId != caregiverUserId)
        {
            return Error.Forbidden("Not authorized to resolve this alert.");
        }

        var resResult = alert.Resolve(caregiverUserId, request.ResolutionNotes);
        if (resResult.IsFailure) return resResult.Error!;

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            alert.SeniorUserId,
            caregiverUserId,
            "Angehöriger",
            "SAFETY_ALERT_RESOLVED",
            "SafetyAlert",
            $"Sicherheitsbenachrichtigung wurde als erledigt markiert: {request.ResolutionNotes}"));

        await _db.SaveChangesAsync(ct);
        return MapAlertToDto(alert);
    }

    public async Task<Result<IReadOnlyList<SafetyAlertDto>>> GetSafetyAlertsForSeniorAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default)
    {
        if (seniorUserId != requesterUserId)
        {
            var hasPermission = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted), ct);

            if (!hasPermission)
            {
                return Error.Forbidden("Not authorized to view safety alerts for this senior.");
            }
        }

        var alerts = await _db.SafetyAlerts
            .Where(a => a.SeniorUserId == seniorUserId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        return alerts.Select(MapAlertToDto).ToList();
    }

    private static FamilyRelationshipDto MapRelationshipToDto(FamilyRelationship r) =>
        new(
            r.Id,
            r.SeniorUserId,
            r.CaregiverUserId,
            r.RelationshipType,
            r.Status,
            r.InvitationCode,
            r.InvitationExpiresAtUtc,
            r.CreatedAtUtc,
            r.ConfirmedAtUtc,
            r.Permissions.Select(p => new FamilyPermissionDto(p.Id, p.PermissionType, p.IsGranted, p.UpdatedAtUtc)).ToList());

    private static TrustedContactDto MapContactToDto(TrustedContact c) =>
        new(
            c.Id,
            c.SeniorUserId,
            c.Name,
            c.PhoneNumber,
            c.Email,
            c.Relationship,
            c.IsPrimaryEmergency,
            c.NotifyOnSafetyAlert,
            c.CreatedAtUtc);

    private static SafetyAlertDto MapAlertToDto(SafetyAlert a) =>
        new(
            a.Id,
            a.SeniorUserId,
            a.TriggeredByUserId,
            a.Category,
            a.Status,
            a.Details,
            a.CreatedAtUtc,
            a.AcknowledgedAtUtc,
            a.AcknowledgedByUserId,
            a.ResolvedAtUtc,
            a.ResolvedByUserId,
            a.ResolutionNotes);
}
