# SeniorConnect — Production Readiness Gate Matrix A–L (2026-10-02)

> Single authoritative matrix for this phase. Status vocabulary is restricted to:
> `PASS` · `BLOCKED` · `NOT YET PROVIDED` · `NOT APPLICABLE`.
> A code implementation is never treated as proof of an external operational
> condition (see §9 evidence discipline in the phase brief).

---

## Summary

| Gate | Area | Status |
| --- | --- | --- |
| A | Credentials / Secrets | **BLOCKED** |
| B | Authentication / Email Provider | **NOT YET PROVIDED** |
| C | Production Infrastructure | **NOT YET PROVIDED** |
| D | Domain / TLS | **NOT YET PROVIDED** |
| E | PostgreSQL Production | **NOT YET PROVIDED** |
| F | Backup | **NOT YET PROVIDED** |
| G | Restore Validation | **NOT YET PROVIDED** |
| H | Observability | **NOT YET PROVIDED** |
| I | Operational Ownership | **NOT YET PROVIDED** |
| J | Safeguarding Operational Ownership | **NOT YET PROVIDED** |
| K | Data Protection / Legal | **NOT YET PROVIDED** |
| L | Participant / Launch Readiness | **NOT YET PROVIDED** |

No gate is `PASS`. No gate is `NOT APPLICABLE` except the RLS sub-item under E.

---

## A — Credentials / Secrets

**Status: BLOCKED**

| Required evidence | State | Evidence |
| --- | --- | --- |
| Exposed credential revoked | **UNVERIFIED** | Not measurable by this phase; no provider-side confirmation supplied. Working tree `sk-` scan clean, but history still contains patterns. |
| New credential generated | **UNVERIFIED** | Not measurable here. |
| Production secret storage configured | **NOT YET PROVIDED** | No secret-manager configuration in the repository. |
| Application references secret provider | Partial / code only | `JWT_SECRET_KEY`, `DB_PASSWORD` read from environment; no provider reference. Code evidence ≠ configured store. |
| No real secret in repository | **BLOCKED** | Working tree clean, but the only reachable repo (`hassanbht/SeniorConnect`) has **44/66 all-ref commits** containing `sk-[A-Za-z0-9]{16,}` (`opencode.json` lines 9, 22). The clean new repo does not exist. |

**Blocker:** the new clean repository is absent and rotation is unverified; per hard
rule 1.2, if the old credential may still be active the gate is BLOCKED.
**Exact human action:** provide the NEW repository URL/path (or its HEAD SHA) and
provider-side confirmation that the old key was revoked and the new key stored in a
secret manager.

---

## B — Authentication / Email Provider

**Status: NOT YET PROVIDED**

| Required evidence | State | Evidence |
| --- | --- | --- |
| Production authentication configuration | Code implemented | Identity module + JWT (`JwtSettings.Resolve`); dev secret fallback refused outside Development. |
| Production email provider | **NOT YET PROVIDED** | `Email:Provider` is configurable; no real provider configured/documented. |
| Verified sender/domain | **NOT YET PROVIDED** | none. |
| Password-reset flow | Code implemented | not externally verified. |
| Account verification flow | Code implemented | Development channel delivers OTP to log; production provider absent. |
| Operational ownership | **NOT YET PROVIDED** | no owner named. |

**Exact human action:** configure and evidence a production email provider with a
verified sender/domain, and name the operational owner.

---

## C — Production Infrastructure

**Status: NOT YET PROVIDED** — no production host/platform, deployment mechanism,
runtime configuration, resource allocation, restart policy or health-check wiring
exists. The repository only provides a buildable container image (validated) and a
`docker-compose.yml` for local/dev use.

**Exact human action:** provision the production host/platform and record the
deployment mechanism, environment variables/secrets, health checks and restart
behavior.

---

## D — Domain / TLS

**Status: NOT YET PROVIDED** — no production domain, DNS record, TLS certificate,
HTTP→HTTPS behavior, or reachable API endpoint through an intended domain exists.

**Exact human action:** register the production domain, issue a valid TLS
certificate, enforce HTTPS, and evidence a reachable endpoint.

---

## E — PostgreSQL Production

**Status: NOT YET PROVIDED** (RLS sub-item: **NOT APPLICABLE**)

| Required evidence | State |
| --- | --- |
| Production database | NOT YET PROVIDED |
| Migration state | Applies automatically on startup (`DB_MIGRATE_ON_STARTUP`, default on); validated against real PostgreSQL locally, no production DB. |
| Connection configuration | Code externalized (`DATABASE_URL` / `DB_*`); no production values. |
| Tenant / RLS configuration | **NOT APPLICABLE** — no `ROW LEVEL SECURITY` / `CREATE POLICY` artifacts exist; isolation is via EF Core fail-closed query filters. |
| Backup configuration | NOT YET PROVIDED (see F) |
| Production access control | NOT YET PROVIDED |

**Exact human action:** provision the production PostgreSQL instance, define access
control, and record connection configuration and migration state.

---

## F — Backup

**Status: NOT YET PROVIDED** — scripts exist (`scripts/backup_db.ps1/.sh`) but no
scheduled backup, target, retention policy, successful artifact or failure
notification exists. Script existence ≠ operational backups.

**Exact human action:** schedule backups to a real target with a retention policy
and a failure notification, and supply a successful backup artifact.

---

## G — Restore Validation

**Status: NOT YET PROVIDED** — no real restore test, no measured recovery, no
application-reconnect validation has been performed against production. A backup
configuration alone is insufficient per the brief.

**Exact human action:** perform and document a real restore test (see
`scripts/restore_db.ps1/.sh`) including measured recovery time and application
reconnect validation.

---

## H — Observability

**Status: NOT YET PROVIDED** — health endpoints exist (`/healthz/live`,
`/healthz/ready`) and structured logging is configured, but no error monitoring,
health monitoring, infrastructure monitoring, log retention or alert ownership is
provided. Health endpoint existing ≠ monitoring active.

**Exact human action:** wire application logs, error/health monitoring, alert
ownership and log retention (with sensitive-data redaction) in the production
environment.

---

## I — Operational Ownership

**Status: NOT YET PROVIDED** — no technical owner, deployment owner, incident owner,
credential-rotation owner or backup/restore owner has been named. No names invented.

**Exact human action:** name the owners for deployment, incidents, credential
rotation and backup/restore.

---

## J — Safeguarding Operational Ownership

**Status: NOT YET PROVIDED** — the **PlatformSafeguardingOfficer** role is the
defined and implemented role for organization-less safeguarding cases (validated by
the S-4 dry-run steps and the safeguarding tests). The operational owner,
escalation path, handling procedure and coverage are not provided. Workflow
implemented ≠ operational ownership assigned.

**Exact human action:** assign the responsible safeguarding role, escalation process,
handling procedure and operational coverage.

---

## K — Data Protection / Legal

**Status: NOT YET PROVIDED** — privacy/DPIA and processing-record documents exist
(`docs/legal/dpia.md`, `docs/legal/verarbeitungsverzeichnis.md`), but no production
privacy review or legal/data-protection approval has been supplied. Location/privacy
behavior is validated in code and by the dry run (assignee sees address under
BR-COMM-04; staff withheld; no grant rows). ADR-026 remains OFF. Legal compliance is
not claimed from code.

**Exact human action:** complete the production privacy review and legal /
data-protection approval, and confirm the ADR-026 stay-OFF decision.

---

## L — Participant / Launch Readiness

**Status: NOT YET PROVIDED** — no deployment-owner authorization, operating
procedures sign-off, support contact/process, participant readiness or explicit
production-activation authorization exists. Early Access remains OFF.

**Exact human action:** obtain explicit production-activation authorization and
complete participant/launch readiness sign-off.

---

## Cross-cutting blocker (affects traceability and gate A)

**The NEW canonical repository is not present.** Only `hassanbht/SeniorConnect`
(private, created 2026-08-22) is reachable, and it is the old, uncleaned repository.
The brief's rule `CURRENT RELEASE CANDIDATE = NEW REPOSITORY HEAD` cannot be
satisfied, so no gate can be raised to `PASS` on repository/security grounds.
