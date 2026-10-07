# Operational Ownership Checklist

> **Status:** 2026-10-02 (launch-security & deployment-closure phase).
> The repository provides **role definitions and their technical
> capabilities**. Actual people are deployment-specific external actions —
> **no names are invented here**. Every row must be filled in writing before
> Early Access. Unknown values are recorded as
> **PENDING — EXTERNAL APPOINTMENT**.

> **Re-verified 2026-10-07 (Controlled Production Deployment phase):**
> all 8 roles still **not appointed / evidence missing** (table below read
> unchanged; no written appointment evidence exists anywhere in the
> repository or was received externally). Operational Ownership = PENDING;
> Safeguarding Operational Ownership = PENDING. This checklist appoints
> nobody.

## Deployment roles

| Role | Person / Organization | Contact | Backup | Status |
| --- | --- | --- | --- | --- |
| **Platform Owner** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Deployment Owner** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Safeguarding Lead** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Platform Safeguarding Officer** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Operational Coordinator** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Support Contact** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Incident Escalation Contact** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |
| **Backup Owner** | _(not known)_ | _(not known)_ | _(not known)_ | PENDING — EXTERNAL APPOINTMENT |

Every cell above is **PENDING — EXTERNAL APPOINTMENT** until the deployment
owner records a real name, contact and deputy. This checklist appoints nobody.

## Role responsibilities (repository basis)

| Role | Responsibility | How the capability is granted |
| --- | --- | --- |
| **Platform Owner** | Overall launch authority for the platform side; final escalation above the deployment owner. | Repository-level administration; delegates the Deployment Owner. |
| **Deployment Owner** | Runs releases/rollbacks, owns configuration and secrets, runs smoke tests on the real deployment. | `PlatformAdmin` capability + deployment environment access + secret store. |
| **Safeguarding Lead** | Works the participating organization's safeguarding cases; imposes/lifts restrictions; converts escalated reports (B-2). | `SafeguardingOfficer` capability + active membership of the organization (`docs/operations/safeguarding-operational-runbook.md` §0). |
| **Platform Safeguarding Officer** | Owns organization-**less** cases (S-4); fail-closed to everyone else while vacant. | `POST /api/v1/safeguarding/platform-officers/{userId}` (PlatformAdmin, audited); org-less `PlatformSafeguardingOfficer` membership. |
| **Operational Coordinator** | Day-to-day operations: matching, rosters, content-free S-2 follow-up, neutral S-3 hold awareness. | `Coordinator` (or `Admin`) role of the organization; coordinator API. |
| **Support Contact** | First contact for participants (not provided by the platform itself). | Named by the operator in onboarding material. |
| **Incident Escalation Contact** | Receives security/safeguarding incidents per `docs/operations/incident-response-runbook.md`. | Escalation path documented; contact data is deployment-specific. |
| **Backup Owner** | Owns backup schedule, retention and restore drills (`docs/operations/backup-restore-requirements.md`). | Access to DB dump + backup storage credentials (external). |

## Rules

- Each role needs a named primary **and** a named deputy before Early Access
  (safeguarding deputies per runbook §10; platform/deployment/backup deputies
  per this checklist).
- One person may hold several roles only if the safeguarding separation
  required by `docs/security/safeguarding-scoping-policy.md` is preserved (an
  ordinary admin never gains safeguarding case access implicitly).
- Appointments are recorded in writing by the Deployment Owner; this
  checklist itself appoints nobody.

**Overall status: PENDING — EXTERNAL APPOINTMENT (all 8 roles).**
