# Release Baseline Evidence (2026-10-07)

This document deliberately separates evidence by **what was validated and
when**, so historical results are never misattributed to the new commit:

1. **Previous candidate validation** — the uncommitted engineering candidate.
2. **Canonical revision validation** — the committed release SHA.
3. **CI evidence** — workflow run for the exact release SHA.
4. **Build evidence** — reproducible build from the committed revision.
5. **Artifact evidence** — artifact identity and source-SHA mapping.

## 1. Previous candidate validation (UNCOMMITTED engineering candidate)

These results were produced against the working tree BEFORE the canonical
commit. They remain valid as candidate evidence and are NOT claimed as
commit-specific proof (see section 2/3 for commit-specific proof).

### Engineering

- `dotnet build backend/SeniorConnect.slnx -c Release`: 0 errors, 0 warnings.
- `dotnet test backend/SeniorConnect.slnx -c Release` with
  `SC_TEST_POSTGRES` against disposable postgres:16:
  Family 50 · PilotHardening 56 · TrustSafety 66 · Community 101 ·
  Organizations 65 · Identity 64 · HelpRequests 222 · Architecture 87 =
  **711 passed / 0 failed / 0 skipped.**

### PostgreSQL

- Environment: disposable `postgres:16` container (`sc-test-pg`), admin
  login `sctest`, per-class scratch databases created+migrated+dropped by
  `PostgresTestDatabase` (repo contract, `SC_TEST_POSTGRES`).
- Previously-skipped Postgres-gated tests (`Phase4PostgresTests`,
  `MigrationRoundTripPostgresTests`, `LocationConsentPostgresTests`):
  executed, all passed — migration chain initializes a clean database
  (head `20261001150947_AddPlatformSafeguardingOfficerRoleCheck`).
- Tenant/matching/safeguarding cross-boundary suites green on the same run.

### Security

Executed: `EndpointSecurityTests` (ProblemDetails bodylessness guard +
server-owned request fields), `TenantIsolationTests` (behavioral:
cross-tenant hidden, community visible), `ApiDocsOptInTests`,
`CorrelationIdValidationTests`, `ReactivationAuditCodeTests`,
`Phase5PolicySweepTests` (incl. ADR-026 OFF), `SafeguardingIsolationTests`,
`SafeguardingLeakSweepTests`, `FunderApiSurfaceTests`,
`OrganizationAccessGuardTests`, `StaffTotpAuthorizationTests`,
`IdAustriaProviderFailClosedTests`, Flutter `wave2_client_boundary_test`.
All green. No secret/OTP/credential logging found by targeted scan; no
private keys, tokens, or real credentials in tree (dev-only documented
defaults only).

### Architecture

87/87 (`SeniorConnect.ArchitectureTests`), incl. module boundaries,
tenant isolation, location-visibility, pilot-independence, reference-data
contracts, no-sensitive-migration-data.

### Flutter

`flutter analyze`: no issues. `flutter test`: 909/909.

### Documentation

Provenance: see `DOCUMENTATION-PROVENANCE-REPORT.md`. Historical source
NOT-IDENTIFIED; current-gate docs reconstructed honestly; nothing
fabricated as historical.

### ADR-026

OFF: `VisitAddress:RequireGrant=false` is the ONLY committed definition
(`appsettings.json`); `appsettings.Production.json` contains no override; no
compose/CI configuration enables it; Flutter has no activation path (no such
client code); the architecture guard in `Phase5PolicySweepTests` was green in
CI run 37658411034 on the release SHA.

## 2. Canonical revision validation (committed release SHA)

```text
Release SHA = 8bf2324b3dea26dec72111567a2d7b35f83bb543 (main)
Commit date = 2026-10-07T19:21:09+02:00
Repository  = https://github.com/hassanbht/SeniorConnect.git (remote main == HEAD)
Working tree at verification = CLEAN (git status --porcelain empty)
```

Equivalence proof between the validated candidate and the committed revision:

- The tracked working tree is byte-identical to HEAD (clean tree), and the
  candidate content that produced the 711/909/87 results is what CI re-executed
  on the exact SHA (section 3): CI per-project counts
  (Community 101 · Family 50 · Architecture 87 · HelpRequests 222 ·
  Organizations 65 · PilotHardening 56 · TrustSafety 66 · Identity 64 = 711)
  exactly match the previously validated local distribution — same projects,
  same counts, 0 failures, 0 skips.
- `docs/` content (release/ADR documentation) is the only candidate content
  NOT in the commit (`.gitignore:190 docs/`, since init) — tracked separately
  as finding RDOCS-1; it does not affect source/test/config equivalence.

## 3. CI evidence (authoritative for the release SHA)

```text
Workflow  = SeniorConnect CI Pipeline (.github/workflows/ci.yml)
Run ID    = 37658411034
Event     = push (branch main)
Head SHA  = 8bf2324b3dea26dec72111567a2d7b35f83bb543  (== release HEAD)
Started   = 2026-10-07T17:21:21Z
Conclusion = success — 3/3 jobs success, 0 failed, 0 skipped
```

- `.NET 10 Backend & Architecture Tests`: restore OK · build Release OK ·
  `dotnet test` → 8 assemblies, **711 passed / 0 failed / 0 skipped**
  (PostgreSQL 16 service + `SC_TEST_POSTGRES` set, so PG-gated tests EXECURED;
  23 Postgres/Npgsql references in the run log; no `Skipped:` lines) ·
  `Security & Dependency Vulnerability Scan (P7-11)` step success
  (per-project "no vulnerable packages"; guard found nothing).
- `Flutter Multi-Matrix & Localization Tests`: `check_locales.py (de/en/fa)`
  success · `flutter analyze` success · `flutter test` → **909 tests passed**.
- `Docker Containerization Build (P9-01)`: backend image built from
  `./backend/Dockerfile`, `push: false` (image not retained).
- Prior runs (context only): 37107586876 (`d0201b5` init) success;
  37366403298 (`8dbdffb`) failure — superseded by the release commit.
- CI security review: 0 `secrets.*` references, no masked/echoed credential,
  no pull-request secret exposure (workflow uses no repository secrets);
  only non-secret CI service defaults inline (postgres:16 dev password).

## 4. Build evidence

- `dotnet restore backend/SeniorConnect.slnx` → OK (2026-10-07).
- `dotnet build backend/SeniorConnect.slnx --no-restore -c Release` from the
  clean HEAD tree → **Build succeeded · 0 Warning(s) · 0 Error(s)**
  (TreatWarningsAsErrors) — reproducible from the committed revision.
- `flutter pub get` → OK; `flutter analyze` (local) → **No issues found**
  (SDK 3.47.6 / Dart 3.13.5). Flutter test suite not re-run locally — CI ran
  909/909 on the exact SHA (engineering regression policy: no blind reruns).
- Backend Release build executed 2026-10-07T17:36Z.

## 5. Artifact evidence

```text
Artifact          = backend/SeniorConnect.Api/bin/Release/net10.0/SeniorConnect.Api.dll
SHA-256           = DECC410C45D44EAB34F4D75B363B3B88AFB12FB9B810DED51F54457978BE61A1
Build configuration = Release (.NET 10.0.401)
Build timestamp   = 2026-10-07T17:36:51Z
Source SHA        = 8bf2324b3dea26dec72111567a2d7b35f83bb543
                   (built from the verified-clean tree == HEAD)
Application version = backend sets no <Version>; Flutter 1.0.0+1
Migration head    = 20261001150947_AddPlatformSafeguardingOfficerRoleCheck
```

- Artifact source SHA == release HEAD SHA → artifact identity **established
  for the local build**.
- **No published artifact exists**: no registry image (CI `push: false`), no
  deployment. Publishing/retaining a release artifact belongs to the future
  deployment phase and remains an external gate.

## External Gates (NOT part of this baseline — unchanged, unevidenced here)

Production hosting/DNS/TLS, SMTP provider, backups, monitoring, legal
approval, participant recruitment, Early Access (NOT STARTED), deployment.
(CI execution is now evidenced for the release SHA — see section 3.)
No claims made about any external gate.
