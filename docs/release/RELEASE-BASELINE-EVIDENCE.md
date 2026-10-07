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
Release SHA = 9ccb5b66021a29c02ddeced4d9ae74d969c476ab (main)
Commit date = 2026-10-07T20:20:23+02:00
Repository  = https://github.com/hassanbht/SeniorConnect.git (remote main == HEAD)
Lineage     = d0201b5 → 8dbdffb → 8bf2324 → b346a1d → 9ccb5b6
Working tree at freeze verification = CLEAN (git status --porcelain empty)
```

Equivalence proof between the previously validated candidate and the current
release revision:

- `git diff 8bf2324..9ccb5b6` across `backend/`, `mobile/`, `tests/`,
  `.github/`, `database/`, `scripts/`, `Dockerfile`, `docker-compose.yml`,
  `pubspec.yaml` is **EMPTY** — the post-baseline commits (`b346a1d`,
  `9ccb5b6`) changed only `.gitignore`, `docs/**` and `prompts/**`.
  **SOURCE BEHAVIOR = UNCHANGED.**
- Because the release SHA is nevertheless NEW, CI was re-executed on the
  exact new SHA (section 3) and independently reproduced the exact validated
  distribution (Community 101 · Family 50 · Architecture 87 ·
  HelpRequests 222 · Organizations 65 · PilotHardening 56 · TrustSafety 66 ·
  Identity 64 = **711/711, 0 skips**; Flutter **909/909**) — historical
  results are not reused as evidence for the new SHA.
- `SOURCE BEHAVIOR = UNCHANGED` and `RELEASE SHA = NEW SHA` are both true and
  recorded deliberately.

## 3. CI evidence (authoritative for the release SHA)

```text
Workflow  = SeniorConnect CI Pipeline (.github/workflows/ci.yml)
Run ID    = 37666080520
Event     = push (branch main)
Head SHA  = 9ccb5b66021a29c02ddeced4d9ae74d969c476ab  (== release HEAD)
Started   = 2026-10-07T18:20:34Z
Conclusion = success — 3/3 jobs success, 0 failed, 0 skipped
```

- `.NET 10 Backend & Architecture Tests`: restore OK · build Release OK ·
  `dotnet test` → 8 assemblies, **711 passed / 0 failed / 0 skipped**
  (PostgreSQL 16 service + `SC_TEST_POSTGRES` set, so PG-gated tests EXECURED;
  zero skipped lines in this run) ·
  `Security & Dependency Vulnerability Scan (P7-11)` step success
  (23 projects reported "no vulnerable packages"; guard triggered 0 times).
- `Flutter Multi-Matrix & Localization Tests`: `check_locales.py (de/en/fa)`
  success · `flutter analyze` success · `flutter test` → **909 tests passed**.
- `Docker Containerization Build (P9-01)`: backend image built from
  `./backend/Dockerfile`, `push: false` (image not retained).
- Run history (context only): 37107586876 (`d0201b5`) success; 37366403298
  (`8dbdffb`) failure — superseded; 37658411034 (`8bf2324`) success;
  37664705350 (`b346a1d`, human documentation commit) success;
  **37666080520 (`9ccb5b6`) success = release evidence**.
- CI security review: 0 `secrets.*` references, no masked/echoed credential,
  no pull-request secret exposure (workflow uses no repository secrets);
  only non-secret CI service defaults inline (postgres:16 dev password).

## 4. Build evidence

- `dotnet restore backend/SeniorConnect.slnx` → OK (2026-10-07).
- `dotnet build backend/SeniorConnect.slnx --no-restore -c Release` from the
  clean `9ccb5b6` tree → **Build succeeded · 0 Warning(s) · 0 Error(s)**
  (TreatWarningsAsErrors) — reproducible from the committed revision
  (executed 2026-10-07T18:22Z; Build Source SHA == Release HEAD).
- `flutter pub get` → OK; Flutter validation on the exact SHA via CI
  (analyze "No issues found" + 909/909); no local test rerun — regression
  policy: documentation-only change, full suite executed by CI on the exact
  new SHA.

## 5. Artifact evidence

```text
Artifact          = backend/SeniorConnect.Api/bin/Release/net10.0/SeniorConnect.Api.dll
SHA-256           = 93D32267B0101854179939A86C0C9DE0BE83494E122E15B1A66AF3BB4A57A461
Build configuration = Release (.NET 10.0.401)
Build timestamp   = 2026-10-07T18:22:27Z
Source SHA        = 9ccb5b66021a29c02ddeced4d9ae74d969c476ab
                   (built from the verified-clean tree == HEAD)
Application version = backend sets no <Version>; Flutter 1.0.0+1
Migration head    = 20261001150947_AddPlatformSafeguardingOfficerRoleCheck
```

- Artifact source SHA == release HEAD SHA → artifact traceability **PASS**.
- The previous artifact identity (`DECC410C…`, built from `8bf2324`) is
  **superseded** and NOT reused for this release SHA.
- **No published artifact exists**: no registry image (CI `push: false`), no
  deployment. Publishing/retaining a release artifact belongs to the future
  deployment phase and remains an external gate.

## External Gates (NOT part of this baseline — unchanged, unevidenced here)

Production hosting/DNS/TLS, SMTP provider, backups, monitoring, legal
approval, participant recruitment, Early Access (NOT STARTED), deployment.
(CI execution is now evidenced for the release SHA — see section 3.)
No claims made about any external gate.
