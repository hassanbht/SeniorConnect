# ADR-026 — Sharing the visit address with the assigned volunteer

Status: **Accepted (Option C, product owner decision 2026-09-30) — implemented as product policy; legal validation required before production activation**
Date: 2026-09-25 (revised 2026-09-30)
Relates to: `BR-COMM-04`, `BR-VISIBILITY-03`, `BR-GEO-06`, ADR-021,
`docs/security/location-consent-and-visibility.md` (Phase 3, decision D-1),
`docs/decisions/ADR-026-implementation-plan.md`

> The repository path requested for this ADR was `docs/architecture/adr/`. The
> repository keeps every ADR in `docs/decisions/` (see `README.md` there and
> `AGENTS.md`), so this ADR lives here too.
>
> Revision 2026-09-30: the product owner approved **Option C**. The grant data
> model, service gate, endpoints and the senior-side Flutter prompt are
> implemented (see `docs/decisions/APPROVED-POLICY-IMPLEMENTATION-REPORT.md`).
> The feature is **disabled by default** (`VisitAddress:RequireGrant = false`):
> BR-COMM-04 stays in force until the Legal/Data Protection Gate is passed and
> an operator explicitly enables the grant flow. No legal compliance, lawful
> basis or GDPR conclusion is claimed or implied anywhere in this ADR.

---

## Context

Two rules now describe what a volunteer learns about where a person lives.

1. **BR-COMM-04 (binding, unchanged).** "Phone numbers and addresses are revealed
   only after an assignment is confirmed, and only to the assigned counterparty —
   never in a public list." BR-VISIBILITY-03 repeats it. BR-GEO-06 says precise
   coordinates are unmasked "only after mutual confirmation of an assignment".
2. **Phase 3 location-sharing consent.** Each person chooses `NONE`,
   `APPROXIMATE` or `PRECISE` (`GET/PUT /api/v1/me/location-sharing`). The
   default is `NONE`. Exact coordinates of a help request reach the assigned
   volunteer only with `PRECISE`.

The code follows both today, which creates the conflict. The assigned volunteer
gets the typed visit address (`HelpRequestDto.LocationAddress`) under BR-COMM-04
even when the senior chose `NONE`. The exact coordinates stay behind
`PRECISE`. A senior who chose "nobody sees where I live" still has the street
address sent to the volunteer. The Phase 3 security review rated this HIGH.

Two different permissions are involved:

- "Share my exact home location for general discovery": a standing,
  person-level setting (Phase 3 consent).
- "Give the volunteer assigned to this visit the minimum address information
  needed to arrive": a per-visit, purpose-bound disclosure.

Treating them as one permission either breaks home visits (Option B) or makes
the Phase 3 setting misleading (the status quo).

### What the code did before Phase 4 (facts)

| Question | Behaviour before Phase 4 |
| --- | --- |
| Who receives `LocationAddress` | Senior, creator, and the user in `AssignedVolunteerUserId` |
| At what step | From `Assigned` on, for every later status |
| After cancellation | **Still the volunteer.** `Cancel()` does not clear `AssignedVolunteerUserId` |
| After completion | Still the volunteer |
| Coordinators / org admins | Never (they are not in the address gate) |
| Blocked pair | Still delivered if a block was created after assignment |
| Audit of address reads | None beyond the generic entity audit |

---

## Options evaluated

### Option A: entering the address counts as consent

Typing a visit address on the request counts as consent to share it with
whoever is assigned.

- **For:** matches BR-COMM-04 as written; no new UI step; home visits keep
  working.
- **Against:** the consent is implicit and unrelated to the Phase 3 setting. A
  family member can enter the address (BR-HELP-04), so the senior never agrees
  personally. It cannot be revoked before the visit.

### Option B: require `PRECISE` location consent for the address

- **For:** one setting and one mental model.
- **Against:** it merges the two permissions above. Every existing senior has
  `NONE` (Phase 3 migrated nobody), so every home visit silently loses its
  address on deployment. To get visits working, seniors would be pushed to share
  exact coordinates for discovery too. That is the opposite of data
  minimisation.

### Option C: a dedicated per-assignment visit-address consent (preferred direction)

When an assignment is confirmed, the senior (or a family member holding
`CanCreateHelpRequests`, BR-HELP-04) is asked once: "Give <volunteer> your
address for this visit?". The answer is stored per assignment.

- **For:** purpose-bound, one visit, one volunteer. It is revocable and
  independent of discovery settings, and matches GDPR Art. 5(1)(b)/(c)
  purpose limitation and minimisation as generally understood.
- **Against:** a new step in the time-critical assignment flow. A senior who
  does not answer blocks the visit. Offline or IVR seniors need a coordinator
  path, which raises the question of whether a coordinator may confirm on the
  senior's behalf. It changes BR-COMM-04 from "revealed after assignment" to
  "revealed after assignment and consent".

### Proposed shape of Option C (for the decision meeting, not implemented)

| Question | Proposal |
| --- | --- |
| Who receives the visit address | Only the currently assigned volunteer of that request |
| Workflow step | Asked at assignment confirmation; delivered only after the answer is "yes" |
| Exact data | The typed visit address (street, number, door/floor hint). Coordinates follow the Phase 3 consent separately |
| Street address or meeting point | Street address for `LocationType = Home`. For public meeting points the address is not personal data and needs no consent |
| One-time or reusable | One-time: bound to (request, volunteer). A new volunteer asks again |
| Revocable before the visit | Yes, until check-in. Revoking after check-in has no practical effect and is not offered |
| After cancellation or unassignment | Access ends immediately (already implemented, see below) |
| Coordinators | Do not see the address. They see postal code and city (grid cell). If an operational need is shown (e.g. a coordinator-driven no-show check), it needs a separate, audited rule |
| Organization admins | Same as coordinators |
| Audit | Grant, revoke and each delivery of the address to the volunteer (`VISIT_ADDRESS_REVEALED`) |
| Privacy | Address is personal data (`DataClass.PersonalData`). Never in lists, exports, logs or notifications |

---

## Decision

### Revision 2026-09-30 — Option C accepted (product decision)

The product owner approved **Option C**: a dedicated, per-assignment,
volunteer-bound, revocable visit-address grant. Implementation summary (details
in `ADR-026-implementation-plan.md` and
`APPROVED-POLICY-IMPLEMENTATION-REPORT.md`):

- Storage: `visit_address_grants` (request, volunteer, granted_by, role,
  granted_at, revoked_at, revoke_reason). No address data is stored — only the
  consent decision.
- Gate: with the feature enabled, the live assignee sees the address only with
  a live grant. Blocks and CONTACT/LOCATION restrictions still withhold first
  (guards only subtract, never add). Staff never receive the address.
- Grant paths: the senior/creator; an authorized representative (BR-HELP-04
  permission); or — for organization requests only, with a mandatory audited
  reason — the coordinator on behalf (`CoordinatorOnBehalf`). This resolves the
  open question about seniors without the app at the product level.
- Revocation: by the grantor or the senior/creator, any time until check-in
  (after check-in the visit happened). Idempotent.
- One-time: bound to (request, volunteer). A new assignment starts without a
  grant. After cancellation or unassignment access ends immediately (Phase 4
  hardening, retained).
- Flutter: the senior's request-status screen shows a grant/revoke card for the
  current assignment (de/en/fa).
- **Feature gate:** `VisitAddress:RequireGrant` ships **false**; an
  architecture guard fails the build if any committed configuration enables it.
  Production activation additionally requires the Legal/Data Protection Gate.
  This ADR makes **no** legal determination; question 1 below remains with
  legal review.

### Historical decision text (superseded 2026-09-30, retained for the record)

**No option was chosen in the 2026-09-25 revision of this ADR.** The repository
held a binding rule (BR-COMM-04) and a preferred direction from the Phase 4
brief (Option C). It held no evidence on how seniors or coordinators react to an
extra consent step at assignment time, and no legal assessment of whether
BR-COMM-04 disclosure is covered by a legal basis other than consent (e.g.
contract performance, Art. 6(1)(b)).

Until the revision above:

1. **BR-COMM-04 stayed in force unchanged.** The assigned volunteer received the
   visit address after assignment, without a separate consent. Phase 3 decision
   D-1 remained open.
2. **Hardening applied in Phase 4.** It does not change who may receive the
   address. It only makes the code match the wording "the assigned
   counterparty":
   - The address, phone and exact coordinates reach the volunteer only while
     the assignment is live (`Assigned`, `InProgress`). After `Cancelled`,
     `Completed`, `NoShow` or `Expired` the former volunteer is no longer "the
     assigned counterparty" and receives none of them.
   - A block between senior and volunteer, or a safeguarding `CONTACT_BLOCKED` /
     `LOCATION_BLOCKED` restriction, withholds address and contact even during
     a live assignment (`docs/security/block-report-policy.md`,
     `docs/security/safeguarding-location-policy.md`).
   - Staff (coordinator, admin, safeguarding officer) still never receive the
     address through these endpoints.
3. **Was not implemented until approval:** the Option C consent record, its
   endpoints and its Flutter prompt.

## Consequences

- Option C is implemented as **product policy** with the feature gate off; the
  BR-COMM-04 baseline stays in force until the Legal/Data Protection Gate
  passes and an operator enables `VisitAddress:RequireGrant`. Legal review is
  **pending** — no compliance claim is made.
- The cancelled-volunteer leak stays closed regardless of the gate.
- BR-COMM-04 and BR-VISIBILITY-03 are re-worded in `business-rules.md` with a
  reference to this ADR (conditional on the feature gate).
- The audit plan (grant, revoke, `VISIT_ADDRESS_REVEALED` on delivery) is
  partially implemented: grant and revoke are auditable entity events; the
  explicit read-audit on address delivery remains open work for the legal gate
  review.

## Questions for product and legal

1. Is the address disclosure under BR-COMM-04 covered by a legal basis other
   than consent? If yes, Option A plus good transparency may suffice.
2. May a family member with `CanCreateHelpRequests` grant the visit-address
   consent on the senior's behalf? May a coordinator, for seniors without the
   app?
3. What happens when the senior does not answer before the visit: reminder,
   coordinator call, or automatic unassignment?
4. Should a coordinator ever see the visit address, e.g. to send a replacement
   volunteer? If yes, under which audited reason?
