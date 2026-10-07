# Phase 7 — Pilot Hardening & Test Gate 7 [COMPLETED & VERIFIED]

## Status: COMPLETE
- All 5 Test Gate 7 criteria satisfied and verified.
- Backend Pilot Hardening: 25/25 tests passing (including 7-day budget simulation and two-tier deletion audit survival).
- Full Backend Solution: 239/239 tests passing across all modules and architecture suites.
- Mobile Client: 590/590 widget and unit tests passing across all 3 locales (de, en, fa), light/dark, and text scales.
- Locales: 100% consistent across de/en/fa (515 keys verified by `check_locales.py`).

---

## Current Status & Gap Analysis
- **Backend Pilot Hardening:**
  - `NotificationBudgetTracker` & `NotificationService` implemented (`BR-NOTIFY-01/02`). Needs automated 7-day week simulation test.
  - `PrivacyService` implements consent recording, data export (JSON), and two-tier deletion (`BR-GDPR-03/04`). Needs end-to-end lifecycle test proving audit survival after export + Tier 2 purge.
  - Rate limiting middleware (`AddRateLimiter`, `UseRateLimiter`) already configured in `Program.cs`.
  - `DataMaintenanceHostedService` executes periodic Tier-2 GDPR purges and verification expirations.
- **Mobile Client:**
  - `MyAppointmentsNotifier` falls back to static demo data on error; needs real local cache persistence (`SharedPreferences`) so real user schedule is retained offline in airplane mode.
  - Emergency contacts / numbers are already available offline in `EmergencyScreen`.
  - Need `PrivacySettingsScreen` and `NotificationPreferencesScreen` for managing consents, exporting data, requesting account deletion, and configuring quiet hours.

---

## Proposed Changes

### 1. Backend: 7-Day Notification Budget Simulation (`P7-02`, Gate 7.5)
In `tests/backend/SeniorConnect.Modules.PilotHardening.Tests/NotificationBudgetTests.cs`:
- Add `SimulatedWeekOfNormalUse_StrictlyEnforcesBudgetCap_Gate7()`:
  Simulate 7 days, with 3 normal non-urgent attempts and 1 critical safety alert per day (28 total dispatches).
  Verify that exactly 2 non-urgent pushes are accepted per 24h rolling day (14 total non-urgent accepted, 7 suppressed), and all 7 critical safety dispatches pass without restriction.

### 2. Backend: E2E GDPR Export, Deletion & Audit Survival (`P7-06`, `P7-07`, Gate 7.2)
In `tests/backend/SeniorConnect.Modules.PilotHardening.Tests/TwoTierDeletionTests.cs`:
- Add `ExportThenDelete_AnonymizesUser_PreservingPseudonymizedAuditTrail_Gate7()`:
  Create user, record audit log, export data (succeeds), request deletion, confirm Tier 1, execute Tier 2 purge, verify user PII is anonymized, and verify audit record still references the user ID with zero PII leakage.

### 3. Mobile: Offline Read Cache Service (`P7-04`, Gate 7.1)
- Create `mobile/senior_connect/lib/core/cache/local_read_cache.dart`:
  Caches key queries (e.g. `/api/v1/community/my-schedule`) to `SharedPreferences`.
- Update `my_appointments_notifier.dart` to save to `LocalReadCache` upon network success and load from cache when offline / on network error.

### 4. Mobile: Privacy & GDPR Settings Screen (`P7-05`, `P7-06`, `P7-07`)
- Create `mobile/senior_connect/lib/features/profile/presentation/privacy_settings_screen.dart`:
  - Manage versioned consents (Terms, Privacy Policy, Push notifications).
  - "Export my personal data" (triggers download / preview of JSON export).
  - "Delete account" (Tier 1 request with 30-day grace period explanation in plain German).
- Add route to `AppRoutes.privacySettings` and wire into `ProfileEditScreen`.

### 5. Mobile: Notification Preferences Screen (`P7-03`)
- Create `mobile/senior_connect/lib/features/profile/presentation/notification_preferences_screen.dart`:
  - Configure quiet hours (Start / End times).
  - Category toggles (Help Requests, Community, Family Welfare).
  - Wire to `GET/PUT /api/v1/notifications/preferences`.

### 6. Operational: Backup & Restore Script (`P7-13`, Gate 7.3)
- Create `scripts/backup_restore_db.ps1` providing tested commands for PostgreSQL `pg_dump` and `pg_restore`.

### 7. Documentation & Checklist Updates
- Move completed Phase 6 plan to `docs/plans/completed/`.
- Update `docs/plans/BUILD-CHECKLIST.md` for Phase 7 and Test Gate 7.
