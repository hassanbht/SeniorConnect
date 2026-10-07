# Repository Integrity Report (2026-10-07 — Canonical Cutover Verification)

Objective Git/CI evidence only. Every value below was produced by an executed
command during the Canonical Repository Cutover Verification phase; nothing is
inferred from documentation.

## Required status block

```text
Git repository = YES
Canonical remote = SeniorConnect → https://github.com/hassanbht/SeniorConnect.git
Branch = main
HEAD = 8bf2324b3dea26dec72111567a2d7b35f83bb543
Working tree = CLEAN
Release candidate committed = YES
Release candidate traceable = YES
Release documents tracked = NO
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
| 8 | `docs/` tracked? | `git ls-files docs/release/` → empty; `git check-ignore -v docs/release/RELEASE-CANDIDATE-MANIFEST.md` → `.gitignore:190:docs/`; `git log --all -- docs/` → empty (never committed in any of the 3 commits) | **NO — finding RDOCS-1** |
| 9 | Release-critical files identifiable? | `docs/release/RELEASE-CRITICAL-FILE-INVENTORY.md` classes A–E checked against `git ls-tree HEAD` (see audit below) | YES (with RDOCS-1 gap) |
| 10 | Secrets excluded? | Targeted scan over all 794 tracked files: no private keys, AWS/GitHub/OpenAI/Slack token patterns; docker-compose secrets are `${ENV:?}` substitutions; `IdAustria.SharedSecret` empty; `opencode.json`/`.env*`/`.serena/project.local.yml` untracked; `ci.yml` has 0 `secrets.*` references | PASS |
| 11 | Canonical identity established? | `git ls-remote --heads SeniorConnect` → `8bf2324… refs/heads/main` == local HEAD; `git remote show SeniorConnect` → up to date; workspace config names the same URL as the intended remote; anonymous API 404 (private) while authenticated access succeeds | ESTABLISHED |
| 12 | What is still missing? | Release documents (`docs/`) are excluded by `.gitignore` (line 190 `docs/`) since the init commit `d0201b5`; no retained/published release artifact (CI image build uses `push: false`) | See findings |

## Release-critical inventory audit (against HEAD `8bf2324`)

| Class | Status in commit | Detail |
| --- | --- | --- |
| A — application/release source | PRESENT | `backend/**` (Api, Application, Domain, Infrastructure, 11 modules), `mobile/senior_connect/**` (lib, assets, pubspec, platform shells), `docker-compose.yml`, `database/` |
| B — tests | PRESENT | `tests/architecture/**` (17 files), `tests/backend/**` (7 assemblies), Flutter tests |
| C — migrations | PRESENT | 19 migration classes + designers + snapshot, head `20261001150947_AddPlatformSafeguardingOfficerRoleCheck` |
| D — documentation | **PARTIAL** | PRESENT: `AGENTS.md`, `backend/AGENTS.md`, `mobile/senior_connect/AGENTS.md`, `backend/README.md`, `00-PRODUCT-BRIEF.md`, `CONTRIBUTING.md`, `SECURITY.md`. **MISSING: `docs/decisions/`, `docs/plans/BUILD-CHECKLIST.md`, `docs/release/`** (all `docs/` excluded by `.gitignore:190` — finding RDOCS-1) |
| E — CI/deployment | PRESENT | `.github/workflows/ci.yml`, `backend/Dockerfile`, `backend/.dockerignore`, `mobile/senior_connect/scripts/check_locales.py`, `analysis_options.yaml` |
| F — local/secret | EXCLUDED | `.env*`, `backend/.env`, `*.user`, `.serena/project.local.yml` — ignored, none tracked |
| G — generated | EXCLUDED | `bin/`, `obj/`, `build/`, `TestResults/` — 0 tracked |
| H — unrelated/history | PRESENT, classified | `prompts/` (5), `skills/` (12), `starter/` (38) tracked — inventory classifies these as history/bootstrap kept in repo per project policy; NOT part of the runtime application baseline |

## Findings

- **RDOCS-1 (release-blocking for document tracking):** `.gitignore` line 190
  (`docs/`) has excluded the entire documentation tree since the init commit.
  `docs/release/`, `docs/decisions/` (ADR documents incl. ADR-027/029/030),
  `docs/plans/BUILD-CHECKLIST.md`, `docs/security/` (incl.
  `credential-exposure-remediation.md`) exist only in the local working tree.
  The ADR-029/ADR-030 implementation and tests ARE committed; only the
  documents are not. Remediation requires a human decision and a human commit
  (this phase must not commit automatically).
- **RDOCS-2 (informational):** the prior CI run on `8dbdffb` (run 37366403298)
  FAILED in the backend job; it was superseded by the release commit
  `8bf2324`, whose run 37658411034 is fully green. The release SHA is the only
  SHA claimed for this baseline.
- **No other release-critical differences** were found between the validated
  candidate and the committed revision: tracked files are byte-identical to
  the working tree (clean tree), and CI on the exact SHA reproduces the exact
  validated test distribution (711 backend/architecture, 909 Flutter).
