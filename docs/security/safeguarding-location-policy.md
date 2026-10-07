# Safeguarding restrictions on discovery, location, contact and assignment (Phase 4)

> Status: implemented 2026-09-25 as a **safety hook**, not a case-management
> workflow. Related: BR-SG-01..06, ADR-004, `verification-and-discovery.md`,
> `location-consent-and-visibility.md`.

## 1. What can be expressed

A safeguarding officer attaches a **restriction** to a safeguarding case they
may see. It restricts the case's subject:

| Flag | Effect |
| --- | --- |
| `DISCOVERY_BLOCKED` (`blocksDiscovery`) | the subject does not appear in discovery or matching, and their requests do not appear to the counterpart |
| `LOCATION_BLOCKED` (`blocksLocation`) | `LocationVisibilityService` returns `NONE` for any location between subject and counterpart, in both directions |
| `CONTACT_BLOCKED` (`blocksContact`) | BR-COMM-04 contact and visit address are withheld between subject and counterpart |
| `ASSIGNMENT_BLOCKED` (`blocksAssignment`) | the subject is not a matching candidate and cannot accept (`403 ASSIGNMENT_NOT_PERMITTED`, no reason given) |

- **Scope.** `counterpartUserId = null` means towards everyone. A set value
  means only between the subject and that one person (a relationship
  restriction).
- **Self is exempt.** Nobody is restricted from seeing their own data.

## 2. Storage and isolation

- The table is `safeguarding.restrictions`, in the isolated schema (ADR-004) and
  the separate `SafeguardingDbContext`. It has an FK to `safeguarding.cases`.
- Check constraints: at least one flag is set, and a lift needs an officer and
  a reason.
- Lifting sets `lifted_at_utc`, the officer and the reason. Rows are never
  deleted (BR-SG-03).
- Other modules read **only** `ISafeguardingRestrictionReader`, which returns
  `ActiveRestriction(subject, counterpart, four flags)`. There is no case id,
  reason, category, severity or officer (BR-SG-05). The domain entity and its
  DTO are on the safeguarding-isolation architecture lists.
- Every officer action (impose, lift, list) writes `safeguarding.access_logs`
  (BR-SG-06).

## 3. API (officer-only)

```
GET  /api/v1/safeguarding/cases/{caseId}/restrictions
POST /api/v1/safeguarding/cases/{caseId}/restrictions
     { counterpartUserId?, blocksDiscovery, blocksLocation, blocksContact, blocksAssignment }
POST /api/v1/safeguarding/restrictions/{restrictionId}:lift   { reason }
```

- The caller needs the `SafeguardingOfficer` capability (403
  `SAFEGUARDING_OFFICER_REQUIRED`).
- The case must be visible to them under the existing rule: their
  organisations' cases, org-less cases, or cases assigned to them. Otherwise it
  is `404`.
- **The tenant and the subject come from the case row**, never from the body.
  An officer of organisation B cannot impose, list or lift a restriction on
  organisation A's case (T30). A coordinator or admin without officer
  membership has no case scope at all.
- A closed or resolved case cannot impose new restrictions.

## 4. No role bypass

There is **no** "safeguarding staff sees everything" exception:

- A safeguarding officer gets no location, address or contact through the
  normal endpoints beyond what any other non-party gets (T29).
- Restrictions only ever **withhold** information. None of them grants access.
- Neither verification nor organisation membership lifts a restriction.

Any future exception (e.g. an officer who needs a visit address during an
emergency) needs its own ADR, a legal basis and an audited, reason-bound
endpoint. That is decision **S-1**.

## 5. Not in scope

Case workflow (triage, notes templates, deadlines), notification of
restrictions to coordinators, automatic restrictions from reports, and legal
exceptions. Those remain later phase and legal/organisational decisions
(**S-2**). Coordinators are not told that a restriction exists; they only see
that a candidate is missing or that an assignment is "not possible". Whether
they should be told is decision **S-3**.

## 6. Review notes

- **S-4 (MEDIUM, pre-existing design, confirm).** An org-less case (a concern
  raised outside any organization) is visible to **every** safeguarding
  officer of **any** organization (`SafeguardingService.VisibleCasesAsync`).
  Phase 4 inherits this, so any officer can restrict the subject of an
  org-less case. Product and safeguarding leads should confirm whether
  org-less cases need a platform-level officer role instead.
- **Event list address (LOW, fails safe).** In the event *list*, only the host
  sees a private venue's address; group managers and organization staff see
  it in the event *detail*. It errs towards hiding.

