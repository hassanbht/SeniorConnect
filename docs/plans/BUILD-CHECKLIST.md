
# BUILD-CHECKLIST — current release gates

> Reconstructed 2026-10-07 during Post-Wave 3 Failure Closure.
> This file records the **current** security/release gates as verified
> in-tree. It does NOT restore the historical 157-task list referenced by
> `backend/README.md` — no Git history exists in this snapshot to restore it
> from, and historical entries are not fabricated here.

## Wave 3 security closure (all green)

- [x] ProblemDetails: no bare 401/403 in `SeniorConnect.Api/Endpoints`
      (`EndpointSecurityTests`)
- [x] Swagger/OpenAPI: explicit `ApiDocs:Enabled` opt-in, OFF by default
      everywhere incl. Staging (`ApiDocsOptInTests`)
- [x] Correlation-ID: validated, bounded, never logged/echoed raw
      (`CorrelationIdValidationTests`)
- [x] Staff TOTP authorization per ADR-029 (`StaffTotpAuthorizationTests`)
- [x] Trust recalculation per ADR-030 (`TrustLevelReaderRecalculationTests`)
- [x] IdAustria provider fail-closed, intentionally retained
      (`IdAustriaProviderFailClosedTests`)
- [x] Volunteer reactivation audited under stable code
      `REACTIVATED_BY_COORDINATOR` (`ReactivationAuditCodeTests`)
- [x] Warnings = 0, errors = 0 (Release build)

## Standing gates (verified this phase)

- [x] Tenant isolation: global query filters + null-org community admission,
      verified behaviorally (`TenantIsolationTests`)
- [x] HelpRequests matching: 219/219 (fixtures seed live trust facts per
      ADR-030, assertions unchanged)
- [x] ADR-026 (`VisitAddress:RequireGrant`) = OFF in every committed config
      (`Phase5PolicySweepTests`)
- [x] Approved policy baseline unchanged: ADR-027 ladder, S-2/S-3/S-4
      content-free/neutral/platform-officer scope, B-1 automatic
      unassignment, B-2 officer-only conversion
- [x] Backend suites green: TrustSafety 66, Organizations 65,
      PilotHardening 56, Family 50, Community 101, Identity 64,
      Architecture 87
- [x] Flutter: `analyze` clean, `flutter test` 909/909
- [x] PostgreSQL integration: PASS 2026-10-07 — disposable postgres:16,
      `SC_TEST_POSTGRES` gate met, 0 failures, 0 skips (222/222 HelpRequests
      incl. previously-gated migration/constraint/consent tests; migration
      head `20261001150947_AddPlatformSafeguardingOfficerRoleCheck` proven
      on empty databases)
- [ ] Localization: NOT-RUN (no locale-affecting change this phase)

## Before Canonical Release Baseline

- [ ] Run Postgres-gated tests against a disposable database
- [ ] Re-run HTTP synthetic validation only if matching/auth/API inputs change
- [ ] Restore historical `docs/` content (product, architecture, roadmap)
      from version-control history when available — not from invention
