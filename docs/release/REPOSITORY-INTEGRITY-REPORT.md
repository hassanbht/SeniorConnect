# Repository Integrity Report (2026-10-07 — Canonical Cutover Verification)

Objective Git/CI evidence only. Every value below was produced by an executed
command during the Canonical Repository Cutover Verification phase; nothing is
inferred from documentation.

## Required status block

```text
Git repository = YES
Canonical remote = SeniorConnect → https://github.com/hassanbht/SeniorConnect.git
Branch = main
HEAD = 9ccb5b66021a29c02ddeced4d9ae74d969c476ab
Working tree = CLASSIFIED — CLEAN at freeze verification; release/gate records
               in docs/release/ + docs/launch/ re-anchored to HEAD afterwards
               (documentation-only edits pending a human commit; no source,
               test, migration, CI or configuration content modified)
Release candidate committed = YES
Release candidate traceable = YES
Release documents tracked = YES — RDOCS-1 CLOSED (114 docs files incl. all 10
               docs/release/ records)
Secret hygiene = PASS
Canonical repository identity = ESTABLISHED
```

## Evidence

| # | Question | Evidence (executed command) | Answer |
| --- | --- | --- | --- |
| 1 | Git repository? | `git status --porcelain` → empty; `git branch --show-current` → `main` | YES |
| 2 | Remote URL | `git remote -v` → `SeniorConnect https://github.com/hassanbht/SeniorConnect.git` (fetch/push) | See block |
| 3 | Branch | `git branch --show-current` → `main` | main |
| 4 | HEAD | `git rev-parse HEAD` → `8bf2324b3dea26dec72111567a2d7b35f83bb543` | See block |
| 5 | Working tree clean? | `git status --short` and `git status --porcelain=v1 --untracked-files=all` → both empty | CLEAN |
| 6 | Release candidate committed? | `git ls-tree -r --name-only HEAD` → 794 files incl. backend, modules, Flutter, tests, 19 migrations, `.github/workflows/ci.yml`, Dockerfile, docker-compose.yml | YES |
| 7 | Release candidate traceable? | CI run 37658411034 `head_sha` == HEAD; CI per-project test counts (101/50/87/222/65/56/66/64 = 711) == previously validated local distribution; Flutter 909/909 in CI | YES |
| 8 | `docs/` tracked? | `git ls-files docs/` → **114 files** incl. `docs/release/` (10), `docs/decisions/` (26), `docs/launch/` (8), `docs/security/` (12), `docs/plans/` (7), `docs/product/` (13); `git check-ignore docs/release/RELEASE-CANDIDATE-MANIFEST.md` → not ignored (narrow `!docs/release/` negation) | **YES — RDOCS-1 CLOSED** |
| 9 | Release-critical files identifiable? | `docs/release/RELEASE-CRITICAL-FILE-INVENTORY.md` classes A–E checked against `git ls-tree HEAD` (see audit below) | YES (with RDOCS-1 gap) |
| 10 | Secrets excluded? | Targeted scan over all 794 tracked files: no private keys, AWS/GitHub/OpenAI/Slack token patterns; docker-compose secrets are `${ENV:?}` substitutions; `IdAustria.SharedSecret` empty; `opencode.json`/`.env*`/`.serena/project.local.yml` untracked; `ci.yml` has 0 `secrets.*` references | PASS |
| 11 | Canonical identity established? | `git ls-remote --heads SeniorConnect` → `8bf2324… refs/heads/main` == local HEAD; `git remote show SeniorConnect` → up to date; workspace config names the same URL as the intended remote; anonymous API 404 (private) while authenticated access succeeds | ESTABLISHED |
| 12 | What is still missing? | No retained/published release artifact (CI image build uses `push: false`); historical integrity findings R-2 (extra key history location, old lineage) and R-3 (no dependency lock/version pin) remain OPEN owner decisions. R-1 and R-4 RESOLVED | See findings |

## Release-critical inventory audit (against HEAD `8bf2324`)

| Class | Status in commit | Detail |
| --- | --- | --- |
| A — application/release source | PRESENT | `backend/**` (Api, Application, Domain, Infrastructure, 11 modules), `mobile/senior_connect/**` (lib, assets, pubspec, platform shells), `docker-compose.yml`, `database/` |
| B — tests | PRESENT | `tests/architecture/**` (17 files), `tests/backend/**` (7 assemblies), Flutter tests |
| C — migrations | PRESENT | 19 migration classes + designers + snapshot, head `20261001150947_AddPlatformSafeguardingOfficerRoleCheck` |
| D — documentation | PRESENT (RDOCS-1 CLOSED) | 114 tracked `docs/` files: `docs/decisions/` (26 incl. ADR-027/029/030), `docs/plans/BUILD-CHECKLIST.md`, `docs/release/` (10 records), `docs/security/` (12 incl. `credential-exposure-remediation.md`), `docs/launch/` (8), `docs/product/` (13), `docs/architecture/` (8), `docs/api/`, `docs/design/`, `docs/legal/`, `docs/operations/`, `docs/onboarding/`, `docs/governance.md`; plus root policy docs (AGENTS.md, 00-PRODUCT-BRIEF.md, CONTRIBUTING.md, SECURITY.md, backend/README.md) |
| E — CI/deployment | PRESENT | `.github/workflows/ci.yml`, `backend/Dockerfile`, `backend/.dockerignore`, `mobile/senior_connect/scripts/check_locales.py`, `analysis_options.yaml` |
| F — local/secret | EXCLUDED | `.env*`, `backend/.env`, `*.user`, `.serena/project.local.yml` — ignored, none tracked |
| G — generated | EXCLUDED | `bin/`, `obj/`, `build/`, `TestResults/` — 0 tracked |
| H — unrelated/history | PRESENT, classified | `prompts/` (5), `skills/` (12), `starter/` (38) tracked — inventory classifies these as history/bootstrap kept in repo per project policy; NOT part of the runtime application baseline |

## Findings

- **RDOCS-1 — RESOLVED (2026-10-07).** Two commits closed it: `b346a1d`
  (human) removed the `.gitignore` `docs/` rule and tracked 104 documentation
  files; `9ccb5b6` added the narrow `!docs/release/` negation against the
  `.gitignore:135 [Rr]elease/` template rule and tracked all 10 release
  records. Secret-ignores re-verified intact (`.env`, `backend/.env`,
  `Document/`, `opencode.local.json`, `.serena/project.local.yml`, `bin/`,
  `obj/`, `Release/` build dirs, `backend/publish/` — all still IGNORED).
- **RDOCS-2 (informational):** the CI run on `8dbdffb` (37366403298) failed and
  was superseded; runs on `8bf2324` (37658411034), `b346a1d` (37664705350) and
  the release SHA `9ccb5b6` (37666080520) are green. Only `9ccb5b6` is claimed
  as the release revision.
- **Source behavior = UNCHANGED.** `git diff 8bf2324..9ccb5b6` across
  backend, mobile, tests, `.github`, database, scripts, Dockerfile,
  docker-compose and pubspec is EMPTY (commits changed only `.gitignore`,
  `docs/**`, `prompts/**`). CI on the exact new SHA independently re-ran the
  full suite: 711/711 (0 skips), 909/909, analyze clean, locale gate, 23
  clean vulnerability-scan lines, Docker build success.
- **Remaining open items (owner decisions, not documentation):** no
  retained/published artifact (`push: false`); integrity findings R-2 (extra
  historical key location on the OLD lineage) and R-3 (no dependency
  lock/`global.json`) stay OPEN.
