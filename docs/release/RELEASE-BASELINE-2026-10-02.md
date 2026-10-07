# SeniorConnect — Release Baseline (2026-10-02)

> **Phase:** Production Readiness Revalidation & External Gate Closure.
> This document is the single release-baseline record for this phase. It records
> only measured values — nothing is inferred or invented. Where a value cannot be
> measured, it is marked **NOT AVAILABLE** and a blocker is raised.

---

## 1. Release manifest

```text
Repository (origin)        : https://github.com/hassanbht/SeniorConnect.git  (PRIVATE)
Canonical repository       : NOT AVAILABLE — the "new clean private repository"
                             described by the brief does not exist in this
                             workspace and is not reachable.  See §7 and §8.
Branch                     : main
HEAD SHA                   : 6f9c5ec1567718ef8a5ff8626070aa1166739a68
Commit date                : 2026-09-29T09:59:52+02:00
HEAD subject               : "P9-07 , P9-10"
Working tree               : DIRTY — 63 modified, 2 deleted, 33 untracked (98 entries)
Application version        : 1.0.0+1   (mobile/senior_connect/pubspec.yaml)
Backend target framework   : net10.0
.NET SDK                   : 10.0.401
Flutter SDK                : 3.47.2 stable (revision d3b14c8769, 2026-08-26)
Dart SDK                   : 3.13.2
Node / tooling             : Node v24.13.1
Database                   : PostgreSQL 16 (postgis/postgis:16-3.4)
Docker image (built here)  : seniorconnect-api:reval  (249 MB, UID 1654, chiseled)
CI workflow                : .github/workflows/ci.yml
Git tags                   : none (0)
GitHub releases            : none (0)
ADR-026 state              : IMPLEMENTED · FEATURE-GATED · DEFAULT OFF · NOT ACTIVATED
                             (VisitAddress:RequireGrant defaults false)
```

---

## 2. Working-tree state

| Field | Value |
| --- | --- |
| Modified | 63 |
| Deleted | 2 |
| Untracked | 33 |
| Total entries | 98 |
| `git diff --shortstat` | 65 files changed, 2114 insertions(+), 552 deletions(-) |
| Commits ahead of `origin/main` | 4 (`6f9c5ec`, `8fd66b1`, `5e75c8b`, `31b9258`) |

The working tree is **not clean**, so a clean release baseline cannot be claimed.
The 65 tracked changes include the credential-remediation edits to `opencode.json`
and `.gitignore`, plus the approved-policy and safeguarding-officer migrations and
module changes listed in the phase workspace.

---

## 3. Toolchain / SDK versions (measured)

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.401 |
| Flutter | 3.47.2 stable (d3b14c8769) |
| Dart | 3.13.2 |
| Node | v24.13.1 |
| Docker | 29.8.0 (build 88096ef) |
| psql client | not on PATH (database probed via container) |

---

## 4. Validation results (this phase)

| Suite | Command | Result | Historical baseline | Match |
| --- | --- | --- | --- | --- |
| Backend (no PostgreSQL) | `dotnet test backend/SeniorConnect.slnx -c Release` | **609 passed / 0 failed / 3 skipped** | — | — |
| Backend (real PostgreSQL) | same, with `SC_TEST_POSTGRES` set | **612 passed / 0 failed / 0 skipped** | 612 / 0 / 0 | ✅ exact |
| Flutter analyze | `flutter analyze` | **No issues found** (22.4 s) | CLEAN | ✅ |
| Flutter tests | `flutter test` | **906 / 906 passed** | 906 / 906 | ✅ exact |
| Localization | `python scripts/check_locales.py` | **OK 713 keys × 3 locales, all consistent** | 713 × 3 | ✅ exact |
| HTTP / synthetic | `python scripts/http_dry_run.py` | **55 PASS / 0 FAIL** | 55 / 55 | ✅ exact |
| Docker production build | `docker build -f backend/Dockerfile backend` | **PASS** (249 MB, non-root 1654, chiseled, no shell) | PASS, 1654, chiseled, no shell, ~249 MB | ✅ exact |
| Container runtime | image against disposable PostgreSQL | **`/healthz/ready` 200 · `/healthz/live` 200** | — | — |

No regression was introduced relative to any historical baseline. No test was
weakened, skipped, deleted, or rewritten.

### Per-assembly backend breakdown (real PostgreSQL)

| Assembly | Passed |
| --- | --- |
| Community | 101 |
| TrustSafety | 48 |
| PilotHardening | 45 |
| Family | 50 |
| Organizations | 54 |
| HelpRequests | 214 (+3 PostgreSQL-gated) |
| Identity | 38 |
| Architecture | 59 |
| **Total** | **612** |

### PostgreSQL-gated security tests

The 3 PostgreSQL-gated tests live in
`tests/backend/SeniorConnect.Modules.HelpRequests.Tests/` (`LocationConsentPostgresTests.cs`
1 × `[PostgresFact]`, `Phase4PostgresTests.cs` 2 × `[PostgresFact]`). All three ran
and passed against real PostgreSQL (`612 / 0 / 0`). They are skipped only when
`SC_TEST_POSTGRES` is unset.

**RLS note:** a repository-wide search for `ROW LEVEL SECURITY` / `CREATE POLICY` /
`current_setting` / `set_config` in `backend/` and `tests/` returned **no matches**.
No PostgreSQL row-level-security policy artifacts exist in this tree. Tenant
isolation is enforced at the EF Core global query-filter layer (fail-closed) and is
covered by the domain/architecture tests inside the 612. The historical
"RLS 10/10" sub-baseline therefore cannot be reproduced as a distinct command and
is **NOT APPLICABLE** to the current tree (see gate matrix).

---

## 5. CI status (CONFIGURED ≠ EXECUTED ≠ PASSED)

| Item | State |
| --- | --- |
| Workflow file present | **CONFIGURED** — `.github/workflows/ci.yml` (backend-checks, mobile-checks, container-checks) |
| Backend build + test job | CONFIGURED (.NET 10, `postgres:16` service, `SC_TEST_POSTGRES`) |
| PostgreSQL integration test job | CONFIGURED |
| Flutter analyze + test job | CONFIGURED |
| Localization check job | CONFIGURED |
| Docker build job | CONFIGURED |
| Last **EXECUTED + PASSED** run | `7e6d1de` on the **old** repository, 2026-09-19T08:27:51Z |
| CI for current HEAD `6f9c5ec` | **NEVER EXECUTED** — the local HEAD and its 4 commits have not been pushed |
| CI for the NEW canonical repository | **BLOCKED** — the new repository does not exist here |

CI is **CONFIGURED** for the new repository only in the sense that the workflow file
is present in the tree. It has not been **EXECUTED** or **PASSED** against the current
HEAD or against any new canonical repository.

---

## 6. Docker image identity

| Property | Value |
| --- | --- |
| Tag | `seniorconnect-api:reval` |
| Size | 249 MB |
| Runtime base | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` |
| Runtime user | `1654` (non-root) |
| Entrypoint | `dotnet SeniorConnect.Api.dll` |
| Shell present | No (chiseled — `sh` not found) |
| Database configuration | Externalized via `DATABASE_URL` / `DB_*` environment |
| Secrets in image | None — `JWT_SECRET_KEY` externalized; dev fallback refused outside Development |
| Runtime health | `/healthz/ready` = 200, `/healthz/live` = 200 |

---

## 7. Release traceability

`docs/release/` now contains:

- `RELEASE-BASELINE-2026-10-02.md` — this document (authoritative baseline).
- `PRODUCTION-GATE-MATRIX-A-L.md` — the single authoritative gate matrix.
- `PRODUCTION-READINESS-FINAL-REPORT-2026-10-02.md` — the phase final report.
- `REVALIDATION-BLOCKED-2026-10-02.md` — prior session's record (historical).
- `RELEASE-CANDIDATE-FREEZE.md`, `RELEASE-CANDIDATE-MANIFEST.md` — earlier candidates (historical).

The **previous repository history and previous commit references are historical
evidence only** and are not the current release candidate.

---

## 8. Known blockers (see gate matrix for full detail)

1. **The new canonical repository is not present.** The only reachable repository is
   `hassanbht/SeniorConnect` (created 2026-08-22), which is the **old, uncleaned**
   repository — its history still contains `sk-` key patterns (`opencode.json`,
   lines 9 and 22 at historical commits). `CURRENT RELEASE CANDIDATE = NEW REPOSITORY
   HEAD` therefore cannot be identified. This blocks the repository/security gates.
2. **Working tree is dirty** (98 entries) — a clean baseline cannot be claimed.
3. **Credential rotation is NOT verified by this phase** (external, provider-side).
4. **All external operational gates (B–L) are NOT YET PROVIDED** — no production
   infrastructure, domain/TLS, database, backup/restore, observability, ownership,
   legal or launch authorization evidence exists in the repository.

---

## 9. ADR-026 state (policy)

```text
IMPLEMENTED
FEATURE-GATED
DEFAULT OFF
NOT ACTIVATED
PENDING EXTERNAL DATA-PROTECTION / LEGAL VALIDATION
```

- `docs/decisions/ADR-026-visit-address-sharing.md` — Accepted (Option C, product
  owner decision 2026-09-30), "legal validation required before production activation".
- `backend/modules/HelpRequests/Infrastructure/VisitAddressOptions.cs` —
  `SectionName = "VisitAddress"`, `RequireGrant` defaults **false**.
- The HTTP dry run confirmed the gate stays OFF: the live assignee sees the address
  under the unchanged BR-COMM-04 baseline, **no** `visit_address_grants` row exists,
  and the exact address remains withheld from organization staff.

No action taken in this phase changed the ADR-026 state.
