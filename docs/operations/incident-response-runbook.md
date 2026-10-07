# Incident Response Runbook — SeniorConnect

> **Status:** 2026-10-02 (launch-security & deployment-closure phase).
> Rewritten to reference **only capabilities that exist in this repository**.
> Where an operational capability does not exist it is marked
> **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT**. Role names are
> placeholders (`docs/operations/operational-ownership-checklist.md`);
> **no statutory or legal reporting deadline is asserted here** — see §9.
>
> This is an engineering runbook. It makes **no legal claim** and names no
> external authority procedurally; escalation contacts are deployment-specific.

## 0. Severity classification

| Severity | Meaning | Target first response |
| --- | --- | --- |
| **SEV-1** | Immediate risk to a person; or total loss of the platform / database. | Immediate (best effort; no automated paging exists — §7). |
| **SEV-2** | Confirmed/credible data exposure, cross-tenant access, or safeguarding-isolation failure. | Same day. |
| **SEV-3** | Degraded but functioning (auth delivery failures, notification outage). | Next working period. |
| **SEV-4** | Cosmetic / low-impact defects. | Backlog. |

Automated paging/monitoring does not exist in the repository
(**NOT IMPLEMENTED / EXTERNAL DEPLOYMENT REQUIREMENT**); detection relies on
health probes, logs and human reports (§8).

## 1. Authentication incident

**Examples:** suspected token/credential compromise, OTP abuse, unusual login
or refresh failures.

- **Detection:** `POST /api/v1/auth/*` error rates; `RATE_LIMITED` (429) spikes;
  startup failures from `JwtSettings.EnsureSafeFor` or
  `EmailProviderOptions.EnsureValidSelection`; operator report.
- **Containment:** a user/operator revokes affected device sessions via
  `POST /api/v1/auth/devices/{id}:revoke` (list via `GET /api/v1/auth/devices`)
  and/or ends the current session with `POST /api/v1/auth/logout`; if the JWT
  signing key is suspected, rotate `JWT_SECRET_KEY` and restart (all existing
  tokens then fail validation — full re-authentication).
- **Escalation:** [Incident Escalation Contact] per §7.
- **Evidence:** application logs (`CorrelationIdMiddleware` correlation id per
  request); `audit_entries` for consent/verification/platform changes. The JWT
  itself and OTP codes are never logged.
- **Recovery:** confirm authentication succeeds for unaffected users and that
  `POST /api/v1/auth/request-email-link` answers 200 through the configured
  provider.
- **Post-incident review:** §10.

## 2. Credential exposure

**Examples:** a secret found in a tracked file, log, or shared location
(cf. `docs/security/credential-exposure-remediation.md`).

- **Detection:** tracked-secret scan; operator report; provider abuse notice.
- **Containment:** stop using the exposed credential; remove it from the
  working tree and replace it with an environment/secret-store reference; the
  owning human revokes/rotates it externally (**rotation is never automated
  here**).
- **Escalation:** [Incident Escalation Contact] + the credential's owner.
- **Evidence:** preserved finding record (e.g. the remediation doc above);
  **do not delete evidence**.
- **Recovery:** confirm the new value works and the old value is rejected;
  re-run the secret scan.
- **Post-incident review:** §10. Note: git-history rewriting is a separate,
  explicitly-approved owner decision — never automatic.

## 3. Unauthorized access

**Examples:** cross-tenant read, an ordinary user reaching safeguarding data,
object-ownership bypass.

- **Detection:** 403/404 anomaly patterns; tenant-isolation regression tests
  failing; reports from an organization.
- **Containment:** these paths are server-side and fail closed (cross-tenant
  reads answer 404; safeguarding is a separate schema). If a real bypass is
  suspected, restrict the deployment to read-only operation via the operator's
  procedure and prepare a rollback (`docs/operations/release-and-rollback-runbook.md`).
  Use `POST /api/v1/blocks` to cut a specific relationship.
- **Escalation:** SEV-2 → [Incident Escalation Contact] + Safeguarding Lead if
  safeguarding data is implicated.
- **Evidence:** `audit_entries`, `safeguarding.access_logs`, request correlation
  ids, and the failing test.
- **Recovery:** deploy a fix; re-run the full authorization/tenant suites; only
  then restore normal operation.
- **Post-incident review:** §10.

## 4. Safeguarding incident

**Examples:** a case that cannot be worked (no reachable officer), suspected
safeguarding-case leakage, block/unassignment failure.

- **Detection:** operator/officer report; failing S-2/S-3/S-4/B-1/B-2 tests;
  `safeguarding.access_logs` anomalies.
- **Containment/response:**
  - org-less cases fail closed while no [Platform Safeguarding Officer] exists —
    appoint one via `POST /api/v1/safeguarding/platform-officers/{userId}`
    (PlatformAdmin, audited) (`docs/operations/safeguarding-operational-runbook.md` §5);
  - assign/close cases via `POST /api/v1/safeguarding/cases/{id}:assign` /
    `:close`; add notes via `/cases/{id}/notes`; impose/lift restrictions via
    `/cases/{id}/restrictions` and `:lift`;
  - any authenticated user can raise a concern via
    `POST /api/v1/safeguarding/concerns`;
  - convert an escalated report via
    `POST /api/v1/community/reports/{reportId}:convert-to-case` (B-2).
  - If a person is in immediate danger, direct them to emergency services
    (144/112 in Austria) — the platform is **not** an emergency service.
- **Escalation:** [Safeguarding Lead] and [Platform Safeguarding Officer] per
  `docs/operations/safeguarding-operational-runbook.md` §9.
- **Evidence:** `safeguarding.access_logs` (per action) and help-request status
  history (assignment releases). Case text never enters `audit_entries` by
  design; preserve the safeguarding schema.
- **Recovery:** confirm an appointed, reachable officer exists and the affected
  case is workable.
- **Post-incident review:** §10.

## 5. Location / privacy incident

**Examples:** exact location disclosed without a valid relationship or consent;
VisitAddress gate enabled unexpectedly; consent revocation not honored.

- **Detection:** location-privacy and BR-DISC-06 tests failing; user report;
  an unexpected `VisitAddress:RequireGrant` value.
- **Containment:** confirm `VisitAddress:RequireGrant=false` is in force
  (reset to `false` and restart if changed —
  `docs/security/visit-address-activation-gate.md`); confirm blocks/restrictions
  only ever subtract; revoke a compromised relationship via
  `POST /api/v1/blocks`.
- **Escalation:** SEV-2 → [Incident Escalation Contact] + [Deployment Owner].
- **Evidence:** access logs, consent records (`consents`,
  `location_sharing_consents`), discovery preferences; keep them.
- **Recovery:** deploy a fix and re-run the location-privacy sweep.
- **Post-incident review:** §10.

## 6. Database incident

**Examples:** database outage/corruption, failed migration, accidental data
loss.

- **Detection:** `/healthz/ready` failing (it includes the database health
  check); startup migration errors in logs; `docker compose ps` unhealthy.
- **Containment:** stop write traffic (stop the `api` container via
  `docker compose stop api`); assess the failing migration before any change.
- **Escalation:** [Backup Owner] + [Deployment Owner].
- **Evidence:** preserve the failing state; take a backup before any recovery
  action (`scripts/backup_db.sh`).
- **Recovery:** restore from a verified dump — `scripts/restore_db.sh <dump>
  <target_db>` after `sha256sum -c` on the dump
  (`docs/operations/backup-restore-requirements.md`). Restoring overwrites
  everything after the backup point; the decision belongs to the
  [Deployment Owner] with the [Backup Owner]. Migrations apply automatically at
  API startup.
- **Post-incident review:** §10.

## 7. Notification outage

**Examples:** OTP/verification email not delivered, push/notification delivery
degraded.

- **Detection:** `EMAIL_PROVIDER_SEND_FAILED` /
  `EMAIL_PROVIDER_NOT_CONFIGURED` responses; provider-side alert; user reports;
  `NotificationMessages` / `NotificationBudgetTrackers` inspection.
- **Containment:** email and SMS adapters **fail closed** — they return a
  provider error instead of pretending delivery, so authentication is never
  weakened. For email, fix or restore the SMTP configuration
  (`docs/operations/email-provider-deployment.md`); a provider that is not
  provisioned is reported as `EMAIL_PROVIDER_NOT_CONFIGURED`.
- **Escalation:** [Deployment Owner] (+ the provider's support, external).
- **Evidence:** application logs (masked recipients only; message bodies that
  carry codes/tokens are never logged).
- **Recovery:** confirm a synthetic send succeeds and the startup validation
  passes.
- **Post-incident review:** §10.

## 8. Detection & evidence sources (actual)

- Health probes: `GET /healthz/live`, `GET /healthz/ready` (database check),
  `GET /health`; container healthcheck `dotnet SeniorConnect.Api.dll --healthcheck`.
- Application logs (structured, per-request correlation id).
- `audit_entries` (state transitions) and `safeguarding.access_logs`
  (safeguarding actions, BR-SG-06) — append-only.
- Per-user data-subject access: `GET /api/v1/me/access-logs`,
  `GET /api/v1/me/export`.
- Monitoring/alerting/on-call paging: **NOT IMPLEMENTED / EXTERNAL DEPLOYMENT
  REQUIREMENT** (only container healthchecks exist).

## 9. Statutory notification

Any statutory notification obligation (e.g. to a supervisory authority or to
data subjects) is a **legal determination**, not an engineering action.

> **PENDING — EXTERNAL LEGAL/OPERATIONAL REVIEW.**

This runbook **does not** state any deadline, threshold or authority. The
accountable owner determines, with qualified advice, whether and when to notify.

## 10. Post-incident review

For every SEV-1/SEV-2 incident, the [Deployment Owner] records (no fixed
deadline is invented here):

1. root cause;
2. what was contained and how;
3. confirmation that audit trails are intact;
4. follow-up regression tests added so the class of defect cannot recur;
5. any documentation or configuration change required.
