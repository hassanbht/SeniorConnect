# Backup & Restore — Operational Requirements

> **Status: EXTERNAL ACTION REQUIRED**
> *(deployment-integration phase, 2026-10-01)*
>
> The previous phase proved the backup/restore **mechanism** inside a
> disposable PostgreSQL environment (synthetic drill PASS). It did **not**
> prove a real operational backup target. No real target exists in this
> repository's scope, and none is invented here.

> **Re-verified 2026-10-07 (Controlled Production Deployment phase):**
> unchanged — Backup = **BLOCKED** (no real target/schedule/owner/monitoring
> exists), Restore = **PENDING** (no authorized target; synthetic drill is
> explicitly NOT counted). No new backup evidence of any kind was received.

## 1. Required capability

| Capability | State | Evidence |
| --- | --- | --- |
| Consistent full backup (`pg_dump -Fc`, compressed) | Implemented | `scripts/backup_db.sh` |
| Integrity verification (SHA-256 per dump) | Implemented | `scripts/backup_db.sh` (`.sha256` sidecar) |
| Restore tooling | Implemented | `scripts/restore_db.sh` |
| Safeguarding schema included in dumps | Implemented | `public` + `safeguarding` schemas; concept `docs/operations/backup-and-restore.md` |
| Mechanism proven end-to-end | Proven (previous phase) | disposable-environment backup → checksum → restore drill PASS |
| **Real operational backup target** | **Does not exist yet** | no target infrastructure is defined or referenced by this repository |

## 2. Required owner

- **[Backup Owner]** — a named person accountable for:
  - the backup schedule actually running,
  - the retention actually applied,
  - executing and recording restore tests,
  - reacting to backup failures.
- Named deputy (see `docs/operations/safeguarding-operational-runbook.md` §10
  for the safeguarding-role deputies).
- The appointment itself is an external, deployment-specific action
  (`docs/operations/operational-ownership-checklist.md`).

## 3. Required credentials

- Read access to the production database for `pg_dump`
  (deployment-supplied `DB_HOST/DB_PORT/DB_NAME/DB_USER` + password — never
  committed).
- Write/read access to the chosen backup storage location.
- Storage encryption credentials (at-rest encryption decision below).
- Access control: who may read backups (they contain all personal data,
  including safeguarding schema content) must be restricted to the
  [Backup Owner] + [Platform Operator] and recorded.

## 4. Required retention decision (external)

- `docs/operations/backup-and-restore.md` proposes daily full dumps,
  30 days rotating, AES-256 at rest, EU storage — **a concept, not a
  deployed fact**.
- The deployment owner must confirm in writing:
  - schedule (frequency & time),
  - retention duration (and legal-hold interaction),
  - storage location & at-rest encryption,
  - off-site/second-copy rule (single-copy backups do not survive
    storage loss).

## 5. Required restore test (on the real target)

Before Early Access with real personal data, one recorded drill against the
**actual** backup target:

1. take a backup with the scheduled mechanism,
2. verify the SHA-256 checksum,
3. restore into a clean, disposable database,
4. verify `__EFMigrationsHistory`, table counts in `public` and
   `safeguarding`, and spot-check synthetic rows,
5. record date, operator and result.

Status: **EXTERNAL ACTION REQUIRED** (previous drill used only a disposable
local environment).

## 6. Failure alerting

- The scripts exit non-zero on failure (`set -euo pipefail`); they do not
  push alerts themselves.
- Alerting on failed/missing backups is provided by whatever schedules them
  (cron/systemd/CI/monitoring) — **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT
  REQUIREMENT**.

## 7. Deployment-specific action list

- [ ] Choose and provision the backup target (storage + schedule runner).
- [ ] Supply credentials out-of-band (secret store; never in the repository).
- [ ] Confirm retention & encryption in writing (§4).
- [ ] Run and record the restore drill on that target (§5).
- [ ] Configure failure alerting (§6).
- [ ] Name [Backup Owner] and deputy.

**Overall status: EXTERNAL ACTION REQUIRED.**
