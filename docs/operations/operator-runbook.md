# Operator Runbook (P9-09)

> **Audience:** the platform operator of a SeniorConnect deployment.
> **Scope:** day-2 operations — health, monitoring, incidents, backups, upgrades,
> tenant lifecycle. Fresh deployment is covered by [deployment-guide.md](deployment-guide.md);
> organization onboarding by [organization-onboarding-guide.md](../onboarding/organization-onboarding-guide.md).
>
> Related: [incident-response-runbook.md](incident-response-runbook.md),
> [backup-and-restore.md](backup-and-restore.md), [ADR-028](../decisions/ADR-028-project-independence.md).

---

## 1. Daily health checks

| Check | How | Healthy looks like |
| --- | --- | --- |
| API liveness | `GET /healthz/live` | 200 |
| Readiness (DB reachable, migrations applied) | `GET /healthz/ready` | 200 |
| DB | `pg_isready -U seniorconnect -d seniorconnect_db` inside the `db` container | `accepting connections` |
| Containers | `docker compose ps` | both services `Up (healthy)` |
| Disk (backups + WAL growth) | host monitoring | < 80% usage |

`scripts/verify_deployment.sh` automates the endpoint + config smoke test.

## 2. Logs & monitoring

- Serilog JSON logs on stdout of the `api` container → `docker compose logs api` or ship to the operator's log store. Production levels are pre-set in [appsettings.Production.json](../../backend/SeniorConnect.Api/appsettings.Production.json) (SQL telemetry at `Warning`).
- Audit trail for state transitions lives in the database (audit schema per [data-model.md §8](../architecture/data-model.md#8-audit-consent-notifications)) — query it, don't reconstruct from logs.
- Watch for: repeated `401` bursts on one account (credential stuffing → rate limiter already active; consider blocking at the proxy), spikes of `409` on assignment endpoints (concurrency), and Postgres deadlocks in logs.

## 3. Routine operations

| Cadence | Task |
| --- | --- |
| Daily | Health checks above; glance at error-rate in logs |
| Nightly | `scripts/backup_db.sh` cron job (compressed `pg_dump -Fc` + SHA-256, per [backup-and-restore](backup-and-restore.md)) |
| Weekly | Restore-verify one backup into a scratch database; check backup retention (30 days) |
| Monthly | Update base images (`docker compose build --pull`); review dependency-vulnerability scan status (CI job, P7-11); review disk + DB size trend |
| Quarterly | DR rehearsal: full restore to a clean host per backup-and-restore §2 (RTO ≤ 30 min, RPO ≤ 24 h); rotate `JWT_SECRET_KEY` if policy requires (forces re-login, no data impact) |
| Per release | Follow deployment-guide §5 (backup → migrate → verify → rollback plan) |

## 4. Tenant (organization) lifecycle

- **Create / onboard:** operator-only step — [organization-onboarding-guide.md](../onboarding/organization-onboarding-guide.md) §3.
- **Suspend:** disable the org's capabilities (org-admin action through the capability model, [authorization.md](../architecture/authorization.md)) — data is retained, tenants are isolated, so no other org is affected.
- **Offboard / delete:** export the org's reports, then delete per [data-retention-policy](../legal/data-retention-policy.md); the safeguarding schema follows its own retention rules and is never dropped casually ([safeguarding-scoping-policy](../security/safeguarding-scoping-policy.md)).
- Cross-tenant isolation is guaranteed at the query level (404, never 403, on foreign-tenant lookups) — regression here is a **P1 incident**.

## 5. Incident handling

1. Triage severity per [incident-response-runbook.md](incident-response-runbook.md) (data breach, safeguarding, availability, correctness).
2. Safeguarding-related reports route to the designated safeguarding officer only — the operator does **not** look into the `safeguarding` schema.
3. For suspected data breach involving personal data: GDPR 72-hour notification clock — legal docs in [docs/legal](../legal/privacy-policy.md).
4. Post-incident: write a short retro (what failed, detection time, fix, follow-up task into BUILD-CHECKLIST).

## 6. Escalation & ownership

This is an independent open-source project (ADR-028): there is no vendor.
The operator is the first and last line. Keep an internal contact list
(operator on-call, safeguarding officer per org, legal/data-protection contact)
next to this runbook — real names/numbers stay out of the repository.
