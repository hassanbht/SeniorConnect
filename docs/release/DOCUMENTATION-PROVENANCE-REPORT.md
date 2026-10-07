# Documentation Provenance Report

> Created 2026-10-07 during Canonical Release Baseline.
> No historical documentation is fabricated here. Anything not proven from
> current in-tree evidence is marked MISSING, not reconstructed.

## Status as of the final canonical release closure (2026-10-07, verified)

```text
Historical source:
NOT IDENTIFIED

Current-gate documentation:
RECONSTRUCTED FROM CURRENT CODE/TEST EVIDENCE

Canonical baseline documentation:
FULLY COMMITTED IN THE NEW REPOSITORY — root/policy documents (AGENTS.md,
00-PRODUCT-BRIEF.md, CONTRIBUTING.md, SECURITY.md, backend/README.md,
prompts/, skills/, starter/) plus the complete docs/ tree: 114 files tracked
as of release SHA 9ccb5b6 (RDOCS-1 CLOSED: .gitignore `docs/` rule removed in
b346a1d; narrow `!docs/release/` negation added in 9ccb5b6).

Historical documentation:
NOT RESTORED — the new Git history contains exactly five commits
(d0201b5 init → 8dbdffb → 8bf2324 → b346a1d → 9ccb5b6). It does NOT restore
ADR-001…ADR-028 as historical records and must never be presented as
historical recovery; docs/decisions/ ADR documents committed here are
current-gate transcriptions (see below) now tracked in the canonical repo.
```

## Historical source

Not found. Searched, in order: no canonical repository accessible, no clean
candidate, no Git history (not a Git repository — no `.git` anywhere),
no local Git objects, no sibling repository, no project archive. The only
evidence that historical docs existed is references inside the tree itself
(`backend/README.md` describes a 17-decision ADR index and a 157-task
checklist; `AGENTS.md`/`CONTRIBUTING.md`/code comments reference
`docs/product/*`, `docs/architecture/*`, `docs/api/*`, `docs/security/*`).

## Proven files

Files whose existence and content are proven by the current tree:

- `AGENTS.md`, `backend/AGENTS.md`, `mobile/senior_connect/AGENTS.md`,
  `backend/README.md`, `00-PRODUCT-BRIEF.md`, `CONTRIBUTING.md`,
  `SECURITY.md`, `prompts/*`, `skills/*`, `starter/*`
- All backend source, tests, migrations, configs; all Flutter source/tests.
- `docs/decisions/ADR-029-*.md`, `docs/decisions/ADR-030-*.md`,
  `docs/plans/BUILD-CHECKLIST.md` — created in prior phases from in-code
  evidence (provenance: CURRENT reconstruction, not historical).

## Reconstructed files (current-gate only)

- `docs/decisions/README.md` — index of verifiable records.
- `docs/decisions/ADR-029-staff-totp-management-authorization.md` —
  transcribed from `Organizations/Contracts/StaffSecurityContracts.cs`,
  `IdentityService.CanManageStaffSecurityAsync`,
  `OrganizationStaffSecurityAuthorizer`, `StaffTotpAuthorizationTests`.
- `docs/decisions/ADR-030-effective-trust-recalculation.md` — transcribed
  from `Identity/Infrastructure/TrustLevelReader.cs`,
  `TrustLevelCalculator`, `TrustLevelReaderRecalculationTests`.
- `docs/plans/BUILD-CHECKLIST.md` — current release gates only.
- `docs/release/*` (this phase) — release traceability from actual runs.

## Missing files (source unavailable — NOT restored, NOT invented)

- `docs/product/*`, `docs/architecture/*`, `docs/design/*`, `docs/api/*`,
  `docs/security/*`, historical `docs/plans/*` (incl. the 157-task
  checklist), ADR-001…ADR-028 documents, `docs/release/*` predecessors.

## Restrictions

Reconstructed files MUST NOT be described as historical documents. They
record current-gate state proven in-tree on 2026-10-07.

## ADR-029 / ADR-030 / BUILD-CHECKLIST provenance

RECONSTRUCTED — CURRENT-GATE ONLY. Numbers ADR-029/030 are established by
consistent in-code references and tests (not invented by the reconstructor);
their documents transcribe, not originate, the decisions.
Historical ADR-001…ADR-028: listed nowhere as recovered — actual evidence
absent. No placeholder ADR files were created.
