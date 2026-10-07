# Safeguarding Operational Runbook

> **Status:** 2026-10-01 (deployment-integration phase review: role
> placeholders aligned to the required four roles; S-3 spelled out
> explicitly). Originally 2026-09-30 (launch-readiness phase). Role-based runbook for the
> safeguarding controls implemented in the approved policy phase (S-2, S-3,
> S-4, B-1, B-2). Role placeholders are used deliberately — the actual
> appointment of named people is an operational gate, not a repository act.
>
> Basis: BR-SG-01…BR-SG-07, `docs/security/safeguarding-scoping-policy.md`,
> `docs/security/phase5-security-gate.md` §16.

## 0. Roles (placeholders)

| Placeholder | Repository capability | Appointed by |
| --- | --- | --- |
| [Operating Organization Safeguarding Lead] | `SafeguardingOfficer` capability, active member of the organization | Operating organization |
| [Platform Safeguarding Officer] | `PlatformSafeguardingOfficer` membership (org-less) | Platform admin, via `POST /api/v1/safeguarding/platform-officers/{userId}` |
| [Operational Coordinator] | `Coordinator` role of the organization | Operating organization |
| [Platform Operator] | `PlatformAdmin` capability | Platform owner |

Nothing in this runbook appoints anyone. Until [Platform Safeguarding
Officer] is appointed, organization-less cases are **inaccessible to
everyone** (fail-closed) — this is the designed behavior, not a defect.

## 1. Who acts on operational tasks (S-2) and the neutral hold (S-3)

**Trigger:** a safeguarding officer imposes a restriction on an
organization-scoped case → the system creates ONE content-free
`OperationalTask` for that organization (kind `SAFEGUARDING_FOLLOW_UP`).
No safeguarding reason, no restriction kind, no case reference, no officer
identity ever appears on it (BR-SG-05; enforced by leak-sweep tests).

**S-3 (neutral indicator):** on the coordinator's volunteer roster
(`GET /api/v1/coordinator/volunteers`) the same subject is marked with the
boolean `OperationalHold` — meaning only "currently unavailable for
operational reasons — contact the organization office". It never carries a
restriction kind, reason, case reference or officer identity (BR-SG-05;
pinned by the S-3 tests and verified content-free in the HTTP dry run).

**Who acts:** the [Operational Coordinator] of that organization sees
"available operational follow-up" surfaces (`OperationalTaskDto`: user id +
display name only) via the coordinator API. The coordinator performs normal
operational follow-up (e.g. roster availability) — **without ever learning
why**.

**Lifecycle:**
- Creation: automatic, inside the restriction transaction; duplicates per
  (organization, subject, open) are prevented.
- Completion: coordinator completes the operational follow-up; recorded
  (who, when).
- Cancellation: lifting the restriction cancels all open tasks for the
  subject automatically (idempotent).

**Boundary:** the task is not a notification channel and carries no payload
beyond subject + display name. Ordinary staff and users never see it.

## 2. Who acts on safeguarding reports (B-2 intake)

Escalated unsafe-behavior reports (member-targeted) surface to the
organization's safeguarding lead as reports flagged
`EscalatedToSafeguarding` — **not** as cases. The lead reviews the report in
the moderation/report surfaces.

## 3. Who may convert reports into cases (B-2)

Only a safeguarding officer who may see the target subject: officer-only
endpoint `POST /api/v1/community/reports/{id}:convert-to-case` (explicit,
manual; severity officer-set from `Low|Medium|High|Critical`, validated
fail-closed; status stays `EscalatedToSafeguarding`; the report keeps
`SafeguardingCaseId` / officer / timestamp traceability; duplicate conversion
→ conflict). **No automatic creation, no automatic notification.**

## 4. Who owns platform-level cases (S-4)

- Organization-attached cases: the owning organization's safeguarding lead
  (strictly tenant-scoped; other organizations get 404).
- Organization-less cases: **only** [Platform Safeguarding Officer].
  Ordinary org officers and admins have no access (fail-closed).
- Appointment/removal: platform admin only, audited
  (`CREATE_/REMOVE_PLATFORM_SAFEGUARDING_OFFICER`); removal suspends the
  membership (BR-SG-03: never deleted).

## 5. What happens if no platform officer exists

Fail-closed: organization-less cases become **inaccessible to everyone**
rather than leaking to ordinary org officers. Emergency procedure:

1. [Platform Operator] appoints a [Platform Safeguarding Officer] (audited).
2. That officer works the case normally.

There is **no** emergency bypass for ordinary org officers, by design.
The runbook owner must keep at least one appointed, reachable platform
officer as part of operational readiness (Legal Gate §3 item).

## 6. Restrictions: impose / lift

- Impose: officer with case visibility, `POST /cases/{id}/restrictions`
  (tenant comes from the case row, never the body; cross-tenant = 404).
  Every imposition creates the S-2 operational task (org cases) and an
  access-log entry.
- Lift: officer with case visibility, `POST /restrictions/{id}:lift`
  (reason required; audited; cancels the subject's open operational tasks).
- Restrictions **only withhold** — they never bypass location consent
  (BR-DISC-06) and never create disclosure.
- Case closure does **not** auto-lift restrictions; lifting is explicit.

## 7. Unassignment handling (B-1)

When either party blocks the other, the live assignment between the pair is
released automatically (status → Open, assignee cleared, audited
status-history row naming only the safety cause — never who blocked whom,
never the block reason). The request returns to the coordinator/open pool
under existing rules; the coordinator re-runs matching normally. No
block-reason notification exists or may be added.

## 8. Accessing audit logs

- Safeguarding actions: `safeguarding.access_logs` (append-only, per action —
  raise, read, note, assign, close, restrict, lift, convert).
- Platform changes and general writes: `audit_entries` via the audit
  interceptor (`CREATE_PLATFORM_SAFEGUARDING_OFFICER`,
  `REMOVE_PLATFORM_SAFEGUARDING_OFFICER`, blocks, reports, consents,
  verifications, discoverability).
- Assignment releases: help-request status history (audited rows).
- Safeguarding case **text** never reaches `audit_entries` (isolated schema,
  no interceptor — deliberate).
- Access path: [Platform Operator] or the owning officer via database access
  under the operator's access-control procedure.

## 9. Emergency escalation path

1. Immediate danger: participants are directed to 144/112 (in-app emergency
   screen; the platform is **not** an emergency service).
2. Safeguarding incident: any authenticated user raises a concern
   (`POST /api/v1/safeguarding/concerns`).
3. If the owning officer is unreachable: [Platform Operator] appoints/
   reassigns officer capability per §5; org cases may be assigned to another
   officer of the same organization.
4. If no platform officer exists for org-less cases: §5 fail-closed procedure.

## 10. Backup owner

Each role in §0 needs a named backup person at deployment time:
[Operating Organization Safeguarding Lead] → named deputy;
[Platform Safeguarding Officer] → second platform officer appointed in
advance (so org-less cases never depend on one person);
[Platform Operator] → named deputy with secret-store access.

Operational gate: these appointments are part of Legal Gate §3 (deployment
readiness) and must exist **before** early access.
