# ADR-026 — Option C implementation plan (APPROVED & IMPLEMENTED, FEATURE-GATED)

Status: **Implemented 2026-09-30** (product owner approved Option C; legal
validation pending — production activation is gated on the Legal/Data
Protection Gate and the `VisitAddress:RequireGrant` flag, which ships **false**).
Implemented behavior deviating from this plan (documented here): the release
audit for a safety unassignment uses the `help_request_status_history` row
(BR-AUDIT-01 pattern) instead of a cross-module `AuditEntry` write — HelpRequests
holds no reference to the Reporting module; the original audit and status
semantics are unchanged.

Relates to: `docs/decisions/ADR-026-visit-address-sharing.md` (Accepted —
Option C), BR-COMM-04, BR-VISIBILITY-03, BR-GEO-06,
`docs/security/block-report-policy.md`,
`docs/product/phase5-decision-matrix.md` (Visit-address item)

> Path note: the brief asked for `docs/architecture/adr/`. The repository keeps
> every ADR in `docs/decisions/` (see `docs/decisions/README.md`), so this plan
> lives here too, next to ADR-026.

## 0. Gate — do not start before all of these are true

1. ADR-026 is promoted from **Proposed** to **Accepted** and records which of
   options A / B / C was chosen.
2. The four legal questions in ADR-026 are answered in writing.
3. BR-COMM-04 and BR-VISIBILITY-03 are re-worded in `business-rules.md` with a
   reference to ADR-026.

Reaching any of these is a signal to stop and report, not to continue.

## 1. Scope of option C

Option C keeps **two permissions separate**:

- *general precise-location consent* (Phase 3, `location_sharing_consents`) — a
  standing person-level setting;
- *per-assignment visit-address sharing* — a one-visit, one-volunteer,
  purpose-bound disclosure.

Option C does **not** change who currently receives the address under
BR-COMM-04 before a decision; it adds a consent step.

## 2. Proposed data model (not created)

| Field | Type | Notes |
| --- | --- | --- |
| `id` | uuid | v7 |
| `help_request_id` | uuid FK | the assignment context |
| `volunteer_user_id` | uuid FK | the single recipient |
| `granted_by_user_id` | uuid FK | senior, or a family member with `CanCreateHelpRequests`, or a coordinator under the separate audited rule |
| `granted_at_utc` | timestamptz | |
| `revoked_at_utc` | timestamptz null | revocation history is kept |
| `revoked_by_user_id` | uuid null | |
| `revoke_reason` | text null | |

- Unique partial index on `(help_request_id, volunteer_user_id) WHERE revoked_at_utc IS NULL`
  — at most one live grant per assignment.
- A new volunteer of the same request needs a **new** grant; the old one is
  revoked, never reused.
- No address is stored on the grant. It remains on the help request; the grant
  only decides whether the existing address gate may release it.

## 3. Lifecycle

| Step | Behaviour |
| --- | --- |
| Asked | at assignment confirmation, to the senior (or the family member who may create help requests) |
| Delivered | only after the answer is "yes" and only while the assignment is live |
| Expiry | ends when the assignment ends (`Cancelled`, `Completed`, `NoShow`, `Expired`) — no separate expiry field is needed |
| Cancellation | access ends immediately (already true today; the grant becomes moot) |
| Reassignment | the new volunteer asks again; the previous grant is revoked |
| Revocation | possible until check-in; after check-in the visit has effectively happened and revocation is not offered |
| Coordinator / admin | never see the address; they keep postal code + city (grid cell) |
| Audit | grant, revoke and each delivery of the address (`VISIT_ADDRESS_REVEALED`) are audit events, with no address payload |

## 4. Integration points (one-line change each, once approved)

1. **Address gate.** `HelpRequestService.MapRequest`'s
   `isAuthorizedToSeeAddress` gains one condition:
   `&& (relationship != AssignedVolunteer || grant exists for (request, volunteer))`.
   Existing block and `CONTACT_BLOCKED`/`LOCATION_BLOCKED` withholding stays in
   front of it and can only subtract.
2. **Endpoint.** A `POST /api/v1/help-requests/{id}/visit-address-grant` and a
   `DELETE …:revoke`, both acting only on the caller's own request or a
   permitted family member. No tenant id in the body; the request row is the
   tenant.
3. **Flutter.** One prompt at assignment confirmation and one opt-out; the
   existing `location_sharing_screen.dart` explanation text is updated to
   describe the new step. Localisation in de/en/fa; RTL verified.
4. **Tests.** Default is no grant; a grant releases the address only while the
   assignment is live; revocation takes effect on the next request; a block
   still withholds; a second volunteer gets nothing; staff never receive it.

## 5. What must NOT happen while the decision is open

- No `visit_address_grants` table or entity, no endpoint, no Flutter prompt.
- BR-COMM-04 stays as written.
- The audit action name `VISIT_ADDRESS_REVEALED` is not introduced.

These are enforced mechanically: `Phase5PolicySweepTests` fails the build if any
`visit_address_grant` / `VisitAddressGrant` / `VisitAddressConsent` artifact
appears in `backend/`, and
`Phase5PolicyClosureTests.VisitAddress_ADR026IsProposed_SoNoOptionCArtifactExistsYet`
fails if a `VisitAddress*` entity type enters the EF model.

## 6. If option A or B is chosen instead

- **Option A** (entering the address is consent): no code change; this plan is
  archived; ADR-026 records the rationale and Phase 3 decision D-1 closes.
- **Option B** (`PRECISE` consent required): the existing consent record is
  reused; the address gate adds `&& consent == PRECISE`. Note that every
  existing senior is at `NONE`, so every home visit would lose its address on
  deployment — ADR-026 records this as the reason it was not preferred.
