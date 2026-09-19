using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Notifications.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;

namespace SeniorConnect.Modules.Family.Infrastructure;

public sealed class FamilyService : IFamilyService
{
    private readonly IFamilyDbContext _db;
    private readonly ISeniorAccountProvisioner _seniorAccountProvisioner;
    private readonly ISupportProfileProvisioner _supportProfileProvisioner;
    private readonly IUserSessionIssuer _userSessionIssuer;
    private readonly IUserContactReader _userContactReader;
    private readonly INotificationDispatcher _notificationDispatcher;

    public FamilyService(
        IFamilyDbContext db,
        ISeniorAccountProvisioner? seniorAccountProvisioner = null,
        ISupportProfileProvisioner? supportProfileProvisioner = null,
        IUserSessionIssuer? userSessionIssuer = null,
        IUserContactReader? userContactReader = null,
        INotificationDispatcher? notificationDispatcher = null)
    {
        _db = db;
        _seniorAccountProvisioner = seniorAccountProvisioner ?? new NoopSeniorAccountProvisioner();
        _supportProfileProvisioner = supportProfileProvisioner ?? new NoopSupportProfileProvisioner();
        _userSessionIssuer = userSessionIssuer ?? new NoopUserSessionIssuer();
        _userContactReader = userContactReader ?? new NoopUserContactReader();
        _notificationDispatcher = notificationDispatcher ?? new NoopNotificationDispatcher();
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
        if (string.IsNullOrWhiteSpace(request.InvitationCode))
        {
            return Error.Validation("Invitation code is required.");
        }

        var trimmedCode = request.InvitationCode.Trim();

        var rel = await _db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.InvitationCode == trimmedCode
                && r.Status == RelationshipStatus.Invited, ct);

        if (rel is null)
        {
            return Error.NotFound("Invitation code");
        }

        if (rel.IsExhausted)
        {
            return Error.Conflict("INVITATION_EXHAUSTED", "This invitation code has been locked due to too many failed attempts.");
        }

        var acceptResult = rel.Accept(caregiverUserId);
        if (acceptResult.IsFailure)
        {
            await _db.SaveChangesAsync(ct);
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
        // Senior themselves, or a caregiver with ManageSettings, can view the
        // roster — it exposes every other caregiver's full permission matrix
        // and pending invitation codes, so mere presence of a relationship
        // (Gate 2: "a family member with no permissions sees literally
        // nothing") is NOT sufficient.
        if (seniorUserId != requesterUserId)
        {
            var canManage = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ManageSettings && p.IsGranted), ct);

            if (!canManage)
            {
                return Error.Forbidden("Not authorized to view caregivers for this senior.");
            }

            // P6-08: Log caregiver roster reads by non-seniors
            _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
                seniorUserId,
                requesterUserId,
                "Angehöriger",
                "VIEW_CAREGIVER_ROSTER",
                "FamilyRelationship",
                "Angehöriger hat die Liste der Betreuungspersonen eingesehen."));
            await _db.SaveChangesAsync(ct);
        }

        var list = await _db.FamilyRelationships
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.SeniorUserId == seniorUserId && r.Status != RelationshipStatus.Revoked)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return list.Select(r => MapRelationshipToDto(r)).ToList();
    }

    public async Task<Result<IReadOnlyList<FamilyRelationshipDto>>> GetSeniorsForCaregiverAsync(
        Guid caregiverUserId,
        CancellationToken ct = default)
    {
        var list = await _db.FamilyRelationships
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.CaregiverUserId == caregiverUserId && r.Status == RelationshipStatus.Active)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var dtoList = new List<FamilyRelationshipDto>(list.Count);
        foreach (var rel in list)
        {
            var contact = await _userContactReader.GetContactAsync(rel.SeniorUserId, ct);
            dtoList.Add(MapRelationshipToDto(rel, contact?.DisplayName, contact?.Phone));
        }

        return dtoList;
    }

    public async Task<Result<ZugangskarteDto>> CreateSeniorWithZugangskarteAsync(
        Guid caregiverUserId,
        CreateSeniorWithZugangskarteRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Error.Validation("Senior display name is required.");

        // 1. Provision real senior User account in Identity module (P6-03)
        var userResult = await _seniorAccountProvisioner.ProvisionSeniorUserAsync(
            request.DisplayName,
            request.PhoneNumber,
            caregiverUserId,
            ct);

        if (userResult.IsFailure)
        {
            return userResult.Error!;
        }

        var seniorId = userResult.Value;

        // 2. Provision SupportProfile in Profiles module (P6-03)
        var profileResult = await _supportProfileProvisioner.ProvisionSupportProfileAsync(
            seniorId,
            request.PostalCode,
            request.City,
            caregiverUserId,
            ct);

        if (profileResult.IsFailure)
        {
            return profileResult.Error!;
        }

        // 3. Generate Zugangskarte with pairing code and QR token
        var zugangskarte = Zugangskarte.Generate(
            seniorId,
            caregiverUserId,
            TimeSpan.FromDays(request.ValidForDays > 0 ? request.ValidForDays : 7));

        // 4. Create initial active relationship between caregiver and senior
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

        return MapKarteToDto(zugangskarte);
    }

    public async Task<Result<ClaimZugangskarteResponse>> ClaimZugangskarteAsync(
        Guid? claimingUserId,
        ClaimZugangskarteRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PairingCode))
        {
            return Error.Validation("Pairing code is required.");
        }

        var trimmedCode = request.PairingCode.Trim();

        var karte = await _db.Zugangskarten
            .FirstOrDefaultAsync(z => z.PairingCode == trimmedCode, ct);

        if (karte is null)
        {
            return Error.NotFound("Zugangskarte");
        }

        if (karte.IsExhausted)
        {
            return Error.Conflict("ZUGANGSKARTE_EXHAUSTED", "This Zugangskarte has been locked due to too many failed attempts.");
        }

        var claimResult = karte.Claim(claimingUserId, request.QrToken);
        if (claimResult.IsFailure)
        {
            await _db.SaveChangesAsync(ct);
            return claimResult.Error!;
        }

        var effectiveUserId = claimingUserId ?? karte.SeniorUserId;

        _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            karte.SeniorUserId,
            effectiveUserId,
            "Senior",
            "ZUGANGSKARTE_CLAIMED",
            "Zugangskarte",
            "Senior hat sich erfolgreich mit der Zugangskarte angemeldet."));

        await _db.SaveChangesAsync(ct);

        // Issue JWT session for the senior account (P6-04)
        var sessionResult = await _userSessionIssuer.IssueSessionAsync(karte.SeniorUserId, "Zugangskarte Device", ct);

        var dto = MapKarteToDto(karte);
        return new ClaimZugangskarteResponse(dto, sessionResult.IsSuccess ? sessionResult.Value : null);
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

            _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
                seniorUserId,
                requesterUserId,
                "Angehöriger",
                "VIEW_ACCESS_LOG",
                "SeniorAccessLog",
                "Angehöriger hat das Zugriffsprotokoll eingesehen."));
            await _db.SaveChangesAsync(ct);
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Abs(days));
        var logs = await _db.SeniorAccessLogs
            .AsNoTracking()
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

            _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
                seniorUserId,
                requesterUserId,
                "Angehöriger",
                "VIEW_TRUSTED_CONTACTS",
                "TrustedContact",
                "Angehöriger hat die Notfallkontakte eingesehen."));
            await _db.SaveChangesAsync(ct);
        }

        var contacts = await _db.TrustedContacts
            .AsNoTracking()
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
        // Senior themselves, or a caregiver with ReceiveSafetyAlerts, can
        // trigger a safety alert — same permission that gates acknowledging
        // and resolving one, so a caregiver revoked of it can't raise one either.
        if (request.SeniorUserId != requesterUserId)
        {
            var hasAccess = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == request.SeniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted), ct);

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

        // P6-06: Dispatch notifications to caregivers who have ReceiveSafetyAlerts permission
        var alertCaregivers = await _db.FamilyRelationships
            .Where(r => r.SeniorUserId == request.SeniorUserId
                && r.Status == RelationshipStatus.Active
                && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted))
            .Select(r => r.CaregiverUserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var caregiverId in alertCaregivers)
        {
            await _notificationDispatcher.DispatchAsync(new NotificationDispatchCommand(
                RecipientUserId: caregiverId,
                Category: "FamilyWelfare",
                Priority: "CriticalSafety",
                Title: "Sicherheitswarnung",
                Body: $"Sicherheitswarnung ({alert.Category}) für Ihren Angehörigen ausgelöst: {alert.Details}",
                PayloadJson: $"{{\"alertId\":\"{alert.Id}\",\"seniorUserId\":\"{alert.SeniorUserId}\"}}"), ct);
        }

        // P6-06: Dispatch direct SMS to trusted emergency contacts who have NotifyOnSafetyAlert enabled
        var emergencyContacts = await _db.TrustedContacts
            .Where(c => c.SeniorUserId == request.SeniorUserId && !c.IsDeleted && c.NotifyOnSafetyAlert)
            .ToListAsync(ct);

        foreach (var contact in emergencyContacts)
        {
            if (!string.IsNullOrWhiteSpace(contact.PhoneNumber))
            {
                await _notificationDispatcher.DispatchDirectSmsAsync(
                    contact.PhoneNumber,
                    $"SeniorConnect Notfallwarnung ({alert.Category}): {alert.Details}",
                    ct);
            }
        }

        if (alertCaregivers.Count > 0 || emergencyContacts.Count > 0)
        {
            _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
                request.SeniorUserId,
                requesterUserId,
                "System",
                "SAFETY_ALERT_NOTIFIED",
                "FamilyRelationship / TrustedContact",
                $"Benachrichtigung an {alertCaregivers.Count} Betreuer und {emergencyContacts.Count} Notfallkontakte versendet."));
            await _db.SaveChangesAsync(ct);
        }

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
            "Sicherheitsbenachrichtigung wurde aufgelöst."));

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
            var hasAccess = await _db.FamilyRelationships
                .AnyAsync(r => r.SeniorUserId == seniorUserId
                    && r.CaregiverUserId == requesterUserId
                    && r.Status == RelationshipStatus.Active
                    && r.Permissions.Any(p => p.PermissionType == PermissionType.ReceiveSafetyAlerts && p.IsGranted), ct);

            if (!hasAccess)
            {
                return Error.Forbidden("Not authorized to view safety alerts for this senior.");
            }

            _db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
                seniorUserId,
                requesterUserId,
                "Angehöriger",
                "VIEW_SAFETY_ALERTS",
                "SafetyAlert",
                "Angehöriger hat die Sicherheitsbenachrichtigungen eingesehen."));
            await _db.SaveChangesAsync(ct);
        }

        var alerts = await _db.SafetyAlerts
            .AsNoTracking()
            .Where(a => a.SeniorUserId == seniorUserId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        return alerts.Select(MapAlertToDto).ToList();
    }

    private static FamilyRelationshipDto MapRelationshipToDto(FamilyRelationship r, string? displayName = null, string? phone = null) =>
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
            r.Permissions.Select(p => new FamilyPermissionDto(p.Id, p.PermissionType, p.IsGranted, p.UpdatedAtUtc)).ToList(),
            displayName,
            phone);

    private static ZugangskarteDto MapKarteToDto(Zugangskarte k) =>
        new(
            k.Id,
            k.SeniorUserId,
            k.CreatedByCaregiverUserId,
            k.PairingCode,
            k.QrPayload,
            k.CreatedAtUtc,
            k.ExpiresAtUtc,
            k.IsClaimed,
            k.ClaimedByUserId);

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

    private sealed class NoopSeniorAccountProvisioner : ISeniorAccountProvisioner
    {
        public Task<Result<Guid>> ProvisionSeniorUserAsync(string displayName, string? phone, Guid createdByUserId, CancellationToken ct = default)
            => Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }

    private sealed class NoopSupportProfileProvisioner : ISupportProfileProvisioner
    {
        public Task<Result> ProvisionSupportProfileAsync(Guid userId, string? postalCode, string? city, Guid? createdByUserId, CancellationToken ct = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class NoopUserSessionIssuer : IUserSessionIssuer
    {
        public Task<Result<UserSessionDto>> IssueSessionAsync(Guid userId, string? deviceLabel = null, CancellationToken ct = default)
            => Task.FromResult(Result<UserSessionDto>.Success(new UserSessionDto("dummy_access_token", "dummy_refresh_token", 900, userId, "Senior")));
    }

    private sealed class NoopUserContactReader : IUserContactReader
    {
        public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<UserContact?>(null);
    }

    private sealed class NoopNotificationDispatcher : INotificationDispatcher
    {
        public Task<Result> DispatchAsync(NotificationDispatchCommand command, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result> DispatchDirectSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
            => Task.FromResult(Result.Success());
    }
}

