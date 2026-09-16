# Phase 6 — Family & Delegation Completion Plan

## Goal
Complete all remaining open items for Phase 6 (P6-03, P6-04, P6-05, P6-06, and 6-digit code Brute-force protection) to fully satisfy Gate 6 criteria and achieve production-readiness for Family & Delegation workflows.

## Non-goals
- Modifying out-of-scope future phases (e.g. Phase 7 offline drift storage, Phase 8 IVR).
- Changing database architectures or adding external message brokers.

## Actors
- **Family Caregiver (e.g. Sabine):** Pre-provisions a senior account, configures initial permissions, receives Zugangskarte code/QR, creates help requests on behalf of the senior, and monitors safety alerts.
- **Senior (e.g. Maria):** Claims Zugangskarte anonymously or with assistance, instantly gets authenticated into an already-populated home screen, reviews and manages delegation permissions.
- **Trusted Contact (e.g. Dr. Müller, neighbor):** Receives immediate SMS alerts when a safety alert is triggered for the senior.

## User Journeys
- `docs/product/user-journeys.md` J1 (Family-led onboarding)
- `docs/product/business-rules.md` BR-FAMILY-01..06, BR-HELP-04, BR-SCOPE-05, BR-AUTH-07

## Proposed Architecture Changes

### 1. Cross-Module Contracts
1. **Identity Contracts (`SeniorConnect.Modules.Identity.Contracts`):**
   - `ISeniorAccountProvisioner`: Allows `FamilyService` to create a real `User` entity (with `SeniorModeDefault = true`, `Status = Active`) for pre-provisioned seniors without violating module boundaries.
   - `IUserSessionIssuer`: Allows issuing JWT Access and Refresh tokens for a user ID upon redeeming a valid Zugangskarte.
2. **Profiles Contracts (`SeniorConnect.Modules.Profiles.Contracts`):**
   - `ISupportProfileProvisioner`: Allows `FamilyService` to create a `SupportProfile` (postal code, city, country) for the newly provisioned senior.
3. **Family Contracts (`SeniorConnect.Modules.Family.Contracts`):**
   - `IFamilyPermissionReader`: Allows `HelpRequests` module to check if a user has active permissions (e.g. `CreateHelpRequestsOnBehalf`) for a senior.
4. **Notifications Contracts (`SeniorConnect.Modules.Notifications.Contracts`):**
   - `INotificationDispatcher`: Allows `FamilyService` to dispatch in-app/push notifications to caregivers and direct SMS to trusted emergency contacts.

### 2. Domain & Entity Updates
1. **`Zugangskarte`:**
   - Add `QrToken` (string), `ClaimedByUserId` (Guid?).
   - Add `FailedAttempts` (short) and `MaxAttempts` (short = 5).
   - Add `IsExhausted => FailedAttempts >= MaxAttempts`.
   - Update `Claim(Guid? claimerUserId, string? qrToken = null)` with failure counter and state transition guards.
2. **`FamilyRelationship`:**
   - Add `FailedAttempts` (short) and `MaxAttempts` (short = 5).
   - Add `IsExhausted => FailedAttempts >= MaxAttempts`.
   - Add `RecordFailedAttempt()`.
   - In `Accept()`, reject if exhausted or expired.

### 3. Application & Infrastructure
1. **P6-03 (Family-led account creation):**
   - `FamilyService.CreateSeniorWithZugangskarteAsync` invokes `ISeniorAccountProvisioner` and `ISupportProfileProvisioner`.
   - Populates real `User` and `SupportProfile`.
2. **P6-04 (Zugangskarte claim & session):**
   - `FamilyEndpoints.cs`: Allow anonymous calls on `/api/v1/family/zugangskarte:claim`.
   - Returns `ClaimZugangskarteResponse` with `ZugangskarteDto` and newly issued `UserSessionDto`.
3. **P6-05 (Help request on behalf of senior):**
   - Add `Guid? SeniorUserId` to `CreateHelpRequestRequest`.
   - `HelpRequestEndpoints.cs` maps `request.SeniorUserId ?? callerUserId`.
   - `HelpRequestService` validates `IFamilyPermissionReader.HasPermissionAsync(caller, senior, "CreateHelpRequestsOnBehalf")`.
4. **P6-06 (Notification dispatch on safety alert):**
   - In `FamilyService.TriggerSafetyAlertAsync`, dispatch notifications to all active caregivers with `ReceiveSafetyAlerts` and SMS to all `TrustedContacts` with `NotifyOnSafetyAlert`.
5. **Brute-Force Protection:**
   - Implement lockout after 5 consecutive failed attempts on invitation codes and pairing codes.

## Test Plan
- Unit tests for brute-force exhaustion on invitation code and pairing code.
- Integration tests for account provisioning via `ISeniorAccountProvisioner` & `ISupportProfileProvisioner`.
- Tests for claiming Zugangskarte issuing session tokens.
- Tests for `CreateHelpRequestAsync` on behalf of senior with permission check and refusal.
- Tests for safety alert notification dispatch.
- NetArchTest verification for module boundaries.
