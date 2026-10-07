# Approved Policy Implementation Report

> **Phase:** Approved Policy Implementation (owner decision pack, 2026-09-30)
> **Date:** 2026-09-30 · **Branch:** main
> **Legal wording:** this report claims engineering completion only. Legal
> review is **pending**; no compliance, lawful-basis or lawyer-approval claim
> is made anywhere in this document or the repository.

# Phase Result

**PASS** — all seven approved product/architecture decisions are implemented,
guarded by tests, and validated by a green Release build and test run. The
only remaining gate is external (ADR-026 activation requires the
Legal/Data Protection Gate); it gates **activation**, not this implementation.

## 1. Approved Decisions

| Decision | Option | Outcome |
| --- | --- | --- |
| ADR-027 | A | Trust ladder ratified: the computed ladder is normative; ADR-027 Accepted; BR-TRUST-02 aligned; verification / trust / safeguarding separation explicit and tested |
| S-2 | B | Content-free operational task created when a restriction is imposed |
| S-3 | B | Coordinators receive only a neutral operational indicator |
| S-4 | B | Dedicated platform-level safeguarding capability owns organization-less cases |
| B-1 | B | Auto-unassign of live assignments on block (deterministic, idempotent, audited) |
| B-2 | A | Manual officer-only conversion of unsafe-behavior reports into cases |
| ADR-026 | C | Visit-address grants, **feature-gated OFF**; activation requires the Legal/Data Protection Gate |

## 2. Implemented

- **ADR-027 (Option A):** ADR-027 status → Accepted (repository ADR
  conventions); BR-TRUST-02 re-worded so the computed ladder
  (`TrustLevelCalculator`) is normative; documented divergence removed;
  verification/trust/safeguarding separation preserved and pinned by tests
  (`Phase5PolicyClosureTests.V3_*`, `Phase5PolicySweepTests`). No change to
  `TrustLevelCalculator`, matching, capability derivation, thresholds or
  existing tests.
- **S-2 (Option B):** `OperationalTask` entity (TrustSafety Domain,
  safeguarding schema, `SAFEGUARDING_FOLLOW_UP` kind). Created inside the
  restriction-imposition transaction for **organization-scoped cases only**
  (org-less/platform cases create no task); carries **no** safeguarding
  reason, no restriction kind, no case reference, no officer identity;
  duplicate prevention per (organization, subject, open) via single-open-task
  check + index `ix_operational_tasks_open_per_subject`; lifting the
  restriction cancels open tasks (idempotent); every action writes
  `SafeguardingAccessLog`. Tenant-scoped by `OrganizationId`.
- **S-3 (Option B):** neutral coordinator surfaces only:
  `VolunteerRosterItemDto.OperationalHold` (bool) and the content-free
  `OperationalTaskDto` (user id + display name), served through
  `ISafeguardingOperationalReader` (TrustSafety **Contracts**). No restriction
  type, reason, case note, officer identity or safeguarding DTO type crosses a
  module boundary (architecture leak-sweep enforced). Not a side channel: the
  flag is boolean and content-free.
- **S-4 (Option B):** `PlatformSafeguardingOfficer` capability
  (`OrganizationMembership.PlatformSafeguardingOfficer`,
  `GetPlatformSafeguardingOfficerUserIdsAsync`); platform-officer
  grant/revoke endpoints; `VisibleCasesAsync` rule: org-attached cases of the
  caller's officer organizations ∪ org-less cases **only for platform
  officers** ∪ cases assigned to the caller. Ordinary org officers no longer
  see org-less cases; org cases stay strictly tenant-scoped; access audited.
  Lifecycle (appointment/removal/fail-closed) in
  `docs/security/safeguarding-scoping-policy.md`.
- **B-1 (Option B):** `IAssignmentBlockEnforcementPort` (TrustSafety
  Contracts) implemented by the HelpRequests module
  (`AssignmentBlockEnforcement`); runs **only inside the first-creation branch
  of `BlockUserAsync`** (idempotent). `ReleaseAssignmentForSafety`:
  Assigned/InProgress → **Open**, assignee/`AssignedAtUtc`/`CheckedInAtUtc`
  cleared, RowVersion++, audited status-history row whose reason names the
  safety cause **without identifying the counterpart**. No block-reason
  notification. All existing protections preserved (discovery, location,
  contact, visit-address withholding, matching, message rules).
- **B-2 (Option A):** `ISafeguardingCaseRaiser` port (TrustSafety Contracts)
  implemented by `SafeguardingService.RaiseCaseAsync` (severity parsed
  fail-closed: `Enum.TryParse` ignoreCase:false + `Enum.IsDefined`, values
  `Low|Medium|High|Critical`). `CommunityService.ConvertReportToCaseAsync`
  (officer-only, escalated unsafe-behavior member-targeted reports only):
  sets `ConvertedAtUtc` / `ConvertedByOfficerUserId` / `SafeguardingCaseId`,
  status stays `EscalatedToSafeguarding`, duplicate conversion →
  `REPORT_ALREADY_CONVERTED` (Conflict), endpoint
  `POST /api/v1/community/reports/{id}:convert-to-case` (201 with `caseId`).
  No automatic creation, no automatic notification.
- **ADR-026 (Option C, feature-gated OFF):** `VisitAddressGrant` entity +
  `visit_address_grants` table (public schema, HelpRequests convention;
  unique live-grant index `ux_visit_address_grants_live` filtered on
  `revoked_at_utc IS NULL`; **stores no address data** — guarded by test);
  `GrantVisitAddressAsync`/`RevokeVisitAddressAsync` behind
  `VisitAddress:RequireGrant` (ships `false`,
  `CoordinatorInterventionHours: 24`); address released to the live assignee
  only while a non-revoked grant exists (volunteer-filtered when assigned);
  senior/creator grant-revoke Flutter card (de/en/fa, RTL-safe, no hardcoded
  strings); HelpRequestDto.`VisitAddressGrantActive`. Architecture guards:
  `VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration`
  (fails the build if any committed configuration enables it) and
  `VisitAddressGrant_StoresNoAddressData`. BR-COMM-04 stays in force until an
  operator enables the gate **after** the Legal/Data Protection Gate.
- **Migration:** `20260930103345_ApprovedPolicyImplementationS2S4B1B2Adr026`
  — creates `operational_tasks` (safeguarding schema) +
  `visit_address_grants` (public) with the indexes above, and adds the three
  B-2 columns to `CommunityReports`. Snapshot diff = exactly those model
  additions.

## 3. Deferred

- **ADR-026 production activation** — engineering complete, activation-gated:
  requires the Legal/Data Protection Gate (BUILD-CHECKLIST Legal Gate §2)
  passed **in writing** for the deployment, including written answers to the
  four open legal questions in ADR-026 (legal basis beyond consent; family/
  coordinator consent on the senior's behalf; no-answer path; coordinator
  address access) before `VisitAddress:RequireGrant` may be set to `true`.
- **PostgreSQL integration test run** — skipped: `SC_TEST_POSTGRES` was not
  set in this environment. **Not executed, therefore not claimed.** The three
  pre-existing CI-only skips in HelpRequests correspond to these tests.
- **Early Access** — NOT STARTED (§11).

## 4. Security Validation

Executed results (Release, `dotnet test backend/SeniorConnect.slnx -c Release`):

- Cross-tenant isolation: green (Organizations 54/54, TrustSafety 48/48 —
  org-case 404 across tenants, restriction cross-tenant refusal).
- Ordinary-staff denial: green (coordinator/admin roles do not satisfy
  officer capability gates; operational-task reads denied to ordinary staff).
- Platform-officer isolation (S-4): green — org-less cases hidden from
  ordinary org officers, visible only via the explicit platform role
  (`Officer_lists_only_own_organisation_cases`, `Phase5PolicyClosureTests.S4_*`).
- Block/unassignment behavior (B-1): green — release to Open + audit row on
  first block; duplicate block leaves no residue (exactly one
  Assigned→Open history row); released volunteer loses request access.
- Unsafe-report conversion authorization (B-2): green — non-officer →
  `Forbidden` + no case raised; non-escalated/non-member-targeted →
  `Validation`; item-target (Post) → refused; duplicate → Conflict, single
  raise.
- Visit-address grant lifecycle (ADR-026): green — gate off → address
  withheld; gate on → only a live grant releases it; revocation ends access;
  grant + block → released volunteer loses the request entirely.
- Blocked-pair address withholding: green (existing Phase 4 tests + new
  block-and-grant interaction test).
- Architecture/security guards: 59/59 (`ModuleBoundaryTests` known-violations
  list unchanged — Matching debt only; leak sweeps, gate-off guard,
  no-address-data guard).
- Coordinator neutral-indicator leakage checks (S-3): green — no restriction
  kind/reason/case reference/officer identity in any coordinator-facing DTO;
  safeguarding DTO types cross no forbidden boundary (architecture sweep).

## 5. Safeguarding Validation

- Safeguarding isolation intact: separate schema, officer-only access,
  `SafeguardingAccessLog` per action (all TrustSafety tests green, 48/48).
- S-2 task content-free: leak-sweep + closure tests assert no reason, no
  restriction kind, no case reference, no officer identity on task rows or
  DTOs; idempotent creation (duplicate prevented).
- S-3 neutrality: coordinator sees only boolean hold / content-free task;
  affected user and ordinary staff denial covered; cross-tenant denied.
- S-4 auditability: platform-officer grant/revoke endpoints and all case
  access remain logged; emergency ownership failure is fail-closed
  (org-less cases with no platform officer are inaccessible rather than
  leaked to org officers).
- B-2 traceability: report→case link (`SafeguardingCaseId`), converting
  officer and timestamp recorded; severity officer-set; reloaded report
  assertions green (Community 101/101).
- B-1 reason non-disclosure: status-history reason names the safety cause
  without identifying which party blocked; no block-reason notification path
  exists (tests green).

## 6. Legal/Data Protection Status

Clearly separated:

1. **Implementation (this repository):** complete — the seven approved
   decisions are implemented and tested.
2. **Legal validation:** **pending** — external to this repository. ADR-026's
   four open legal questions and the BUILD-CHECKLIST Legal Gate §2 items
   require qualified legal review for the actual operating entity. No legal
   conclusion is drawn anywhere in this report.
3. **Deployment readiness:** separate operational checklist (Legal Gate §3:
   written sign-offs, named safeguarding lead per community, named platform
   safeguarding officer, rehearsed backup/restore, current incident runbook,
   early-access entry gate).

Permitted wording only is used throughout: "legal review pending",
"requires deployment-specific validation", "implemented as product policy;
legal validation required before production activation".

## 7. Operational Status

Named responsibilities:

- **Operating organization:** signs the DPA, appoints its community
  safeguarding lead (`SafeguardingOfficer`), keeps the ADR-026 gate OFF
  unless Legal Gate §2 passed in writing.
- **Operator/platform owner:** appoints the **platform safeguarding officer**
  (S-4) for organization-less cases; owns deployment readiness sign-off.
- **Safeguarding lead (per community):** owns conversions (B-2), restrictions
  and the resulting S-2 operational tasks.
- **Coordinators:** act on neutral S-3 indicators only; no safeguarding
  facts reach this role.

Remaining operational gates: Legal Gate §2 (legal validation, in writing);
Legal Gate §3 (deployment readiness); platform-officer appointment;
`SC_TEST_POSTGRES` run of the PostgreSQL integration suite in a
Postgres-enabled environment.

## 8. Tests

Exact commands and exact executed results:

- `dotnet build backend/SeniorConnect.slnx -c Release --nologo`
  → Build succeeded; **0 errors** (16 warnings, all pre-existing).
- `dotnet test backend/SeniorConnect.slnx -c Release --no-build --nologo`
  → exit code 0. **595 passed / 0 failed / 3 skipped (pre-existing
  Postgres-CI-only)**:
  Organizations 54 · TrustSafety 48 · Community 101 · PilotHardening 45 ·
  Family 50 · HelpRequests 214/217 (3 skipped) · Identity 24 ·
  Architecture 59.
- `dotnet test backend/SeniorConnect.slnx` (Debug, full suite, earlier in
  phase) → identical counts: 595 passed / 0 failed / 3 skipped.
- `flutter analyze` → **No issues found!**
- `flutter test` → **906 passing** (the one known cosmetic
  `[Easy Localization] key not found` warning per run is the documented,
  deliberately untouched fallback contract).
- `python scripts/check_locales.py` → **OK: 713 keys, 3 locales, all
  consistent.**
- PostgreSQL integration tests: **NOT executed** (`SC_TEST_POSTGRES` absent)
  — explicitly skipped, not claimed.

## 9. Files Changed

Implementation (new):

- backend/SeniorConnect.Infrastructure/Migrations/20260930103345_ApprovedPolicyImplementationS2S4B1B2Adr026.cs
- backend/SeniorConnect.Infrastructure/Migrations/20260930103345_ApprovedPolicyImplementationS2S4B1B2Adr026.Designer.cs
- backend/modules/HelpRequests/Domain/VisitAddressGrant.cs
- backend/modules/HelpRequests/Infrastructure/AssignmentBlockEnforcement.cs
- backend/modules/HelpRequests/Infrastructure/VisitAddressGrantConfiguration.cs
- backend/modules/HelpRequests/Infrastructure/VisitAddressOptions.cs
- backend/modules/TrustSafety/Domain/OperationalTask.cs
- backend/modules/TrustSafety/Infrastructure/SafeguardingOperationalReader.cs

Implementation (modified):

- backend/SeniorConnect.Api/Endpoints/CommunityEndpoints.cs
- backend/SeniorConnect.Api/Endpoints/CoordinatorEndpoints.cs
- backend/SeniorConnect.Api/Endpoints/HelpRequestEndpoints.cs
- backend/SeniorConnect.Api/Endpoints/SafeguardingEndpoints.cs
- backend/SeniorConnect.Api/Program.cs
- backend/SeniorConnect.Api/appsettings.json (VisitAddress section; gate false)
- backend/SeniorConnect.Infrastructure/SeniorConnectDbContext.cs
- backend/SeniorConnect.Infrastructure/Migrations/SeniorConnectDbContextModelSnapshot.cs
- backend/modules/Community/Application/Dtos.cs
- backend/modules/Community/Application/ICommunityService.cs
- backend/modules/Community/Domain/CommunityReport.cs
- backend/modules/Community/Infrastructure/CommunityService.cs
- backend/modules/HelpRequests/Application/Dtos.cs
- backend/modules/HelpRequests/Application/IHelpRequestService.cs
- backend/modules/HelpRequests/Application/IHelpRequestsDbContext.cs
- backend/modules/HelpRequests/Domain/HelpRequest.cs
- backend/modules/HelpRequests/Infrastructure/HelpRequestService.cs
- backend/modules/HelpRequests/Infrastructure/HelpRequestsModuleExtensions.cs
- backend/modules/Organizations/Contracts/OrganizationsContracts.cs
- backend/modules/Organizations/Domain/OrganizationMembership.cs
- backend/modules/Organizations/Infrastructure/OrganizationCoordinatorReader.cs
- backend/modules/TrustSafety/Application/ISafeguardingDbContext.cs
- backend/modules/TrustSafety/Contracts/TrustSafetyContracts.cs
- backend/modules/TrustSafety/Infrastructure/SafeguardingConfigurations.cs
- backend/modules/TrustSafety/Infrastructure/SafeguardingDbContext.cs
- backend/modules/TrustSafety/Infrastructure/SafeguardingService.cs
- backend/modules/TrustSafety/Infrastructure/TrustSafetyModuleExtensions.cs
- backend/modules/TrustSafety/Infrastructure/TrustSafetyService.cs
- mobile/senior_connect/lib/features/help_requests/application/my_request_status_notifier.dart
- mobile/senior_connect/lib/features/help_requests/presentation/my_request_status_screen.dart
- mobile/senior_connect/assets/translations/de.json
- mobile/senior_connect/assets/translations/en.json
- mobile/senior_connect/assets/translations/fa.json

Tests:

- tests/architecture/SeniorConnect.ArchitectureTests/Phase5PolicySweepTests.cs
- tests/backend/SeniorConnect.Modules.HelpRequests.Tests/Phase5PolicyClosureTests.cs
- tests/backend/SeniorConnect.Modules.HelpRequests.Tests/Phase4VerificationDiscoveryTests.cs
- tests/backend/SeniorConnect.Modules.TrustSafety.Tests/TrustSafetyAuthorizationTests.cs
- tests/backend/SeniorConnect.Modules.Community.Tests/B2ReportToSafeguardingCaseConversionTests.cs (new)

Documentation:

- docs/decisions/ADR-026-visit-address-sharing.md
- docs/decisions/ADR-026-implementation-plan.md
- docs/decisions/ADR-027-trust-level-definition.md
- docs/decisions/README.md
- docs/decisions/APPROVED-POLICY-IMPLEMENTATION-REPORT.md (this file)
- docs/product/business-rules.md
- docs/product/phase5-decision-matrix.md
- docs/product/early-access-validation-plan.md
- docs/security/safeguarding-scoping-policy.md
- docs/security/phase5-security-gate.md
- docs/plans/BUILD-CHECKLIST.md (Legal Gate redesign + 2026-09-30 status block)

Classification (per brief §17): **A** implementation, **B** tests,
**C** documentation. **No D (unrelated) changes were introduced by this
phase.** Pre-existing unrelated working-tree state from earlier closure
phases (e.g. `.agents/rules/*`, `AGENTS.md`, `00-PRODUCT-BRIEF.md`,
`docs/product/roadmap.md`, `docs/plans/active/*`, earlier decision-pack
documents) was left untouched, per git discipline.

## 10. Remaining Blockers

- **Legal/Data Protection Gate (external)** — the only genuine blocker, and
  only for ADR-026 production activation. Nothing blocks the engineering
  baseline delivered by this phase.

## 11. Early Access Status

**NOT STARTED.**

No early-access launch, partner contact, second-organization onboarding, or
production visit-address sharing has occurred or is authorized by this
report. The next roadmap phase does not begin from this document.
