# SeniorConnect Owner Decision Pack

> Prepared: 2026-09-30 · Phase: Owner Decision Resolution & Implementation Specification.
> **Audience: the human owner and the named stakeholders — not a developer.**
> This pack contains no recommendations, no rankings, and no pre-made choices.
> Every "Selected option" field starts as **PENDING**. Nothing in this pack changes
> code, ADR status, security behaviour, or the Legal Gate.
>
> Companion documents (historical, kept): `DECISION-PACK-NEXT.md` (audit inventory),
> `DECISION-IMPLEMENTATION-REPORT.md` (verification that nothing was implemented).

---

## Executive Summary

- **All seven decisions are still pending.** No decision has been made by the
  agent, and none was approved by the owner as of this pack's date.
- **No implementation is blocked by technical failure.** The platform builds and
  tests green (586 backend / 906 Flutter / 58 architecture incl. independence
  guard 2/2 / 705×3 locales). The blockers are **missing product/legal decisions**,
  not engineering work.
- **Early Access cannot begin** until the gates in the graph below are satisfied
  (Legal Gate, willing community, and the decisions that feed them).
- Each decision below gives you the options exactly as they exist in the
  repository, the consequences of each, and a fill-in record template. You choose;
  a later implementation phase converts the choices into code/docs/tests.

### Decision dependency graph (repository-established facts only)

```
ADR-027 (V-3) ──────────────► trust documentation/policy alignment (docs-only if A)

S-2 + S-3 + B-2 ────────────► safeguarding operational policy (officer surface)

S-3 ──► informs ──► B-1 ────► blocking/assignment lifecycle policy

S-4 ────────────────────────► SECOND-ORGANIZATION readiness gate

ADR-026 + its 4 legal questions ──► visit-address policy (+ Legal Gate input)

ALL owner decisions ──► implementation phase(s) ──► Legal Gate ──► willing
community ──► Early Access
```

**Which decision gates what (do not over-generalize):**

| Gate | Blocked by |
| --- | --- |
| Trust documentation alignment | ADR-027 only |
| Safeguarding officer surface improvements | S-2, S-3, B-2 |
| Block/assignment lifecycle change | B-1 (informed by S-3) |
| **Onboarding a second organization to any deployment** | **S-4 (hard gate)** |
| Visit-address policy change / ADR-026 closure | ADR-026 + its 4 legal answers |
| **Early Access** | Legal Gate + willing community + written risk acceptance of still-open decisions (per `early-access-validation-plan.md`). The repository does **not** establish that every one of the seven decisions must be resolved first — each is risk-accepted or resolved in writing by the operating organization. |

---

## 1. ADR-027 — Trust level definition (V-3)

**Current state:** ADR-027 is **Proposed**. The running system computes a 0–5
trust ladder (`TrustLevelCalculator`); the binding rule table BR-TRUST-02
describes a different *assurance* ladder. Matching and `PerformSafetyLevelN`
capabilities consume the computed ladder; the disclosure layer reads neither.
Architecture tests keep the ladders separate. **No implementation change is
currently allowed** until you choose.

### Decision — choose one:

**Option A. Ratify the existing computed trust ladder as documented.**
- What changes: BR-TRUST-02's table is reworded to the computed ladder (the ADR
  already contains the proposed wording); the ADR moves Proposed → Accepted.
- What does not change: any code, any test, matching behaviour, capabilities.
- Implementation impact: none (docs-only ratification in a later phase).
- Documentation impact: business-rules.md divergence note closes; ADR index updated.
- Test impact: none; existing separation tests remain and keep passing.
- Dependencies: none — this decision is independent of all others.

**Option B. Request a new trust-level design/alignment phase.**
- What changes: nothing now; a future phase would align code to the original
  BR-TRUST-02 table (or another design you specify).
- What does not change: current behaviour until that new design is approved.
- Implementation impact: a dedicated phase (capability remapping, trust-snapshot
  backfill, matching-threshold review, test updates) — the ADR estimates this as
  its own phase, not a quick fix.
- Documentation impact: BR-TRUST-02 divergence note stays open until then.
- Test impact: existing tests keep passing unchanged.
- Dependencies: requires a new approved design before any code work.

### Decision record

```
Decision ID: ADR-027 (V-3)
Owner: product owner
Additional required stakeholder: technical lead
Selected option: PENDING          (A | B)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 2. S-2 — Restriction notification / workflow

**Current behavior:** an officer imposes/removes a restriction through the API;
the action is written to `SafeguardingAccessLog` (BR-SG-06); the restriction
takes effect immediately for every reader; **no notification is sent and no
coordinator task is created**. Restriction kinds today: `DISCOVERY_BLOCKED`,
`LOCATION_BLOCKED`, `CONTACT_BLOCKED`, `ASSIGNMENT_BLOCKED`.

### Decision — choose one:

**Option A. Officer-only record; no notification.** (Status quo, formalized.)
- Changes: none to code; the rule text states that silence is the policy.
- Privacy impact: none. Safeguarding impact: none. Operational impact: officers
  must check their own queue; nothing pushes information outward.

**Option B. Create an operational task without revealing safeguarding details.**
- Changes (later phase): a content-free operational task/flag appears for a
  defined operational role when a restriction is imposed.
- Privacy impact: low if and only if the task carries no safeguarding facts —
  the content boundary must be specified in the decision.
- What must be specified if chosen: task recipient role, task content
  (none/reason-free), creation trigger, completion path.

**Option C. Notify the affected person and/or a designated operational role.**
- Changes (later phase): a notification workflow with delivery semantics.
- Privacy impact: highest of the three — restriction details may leave the
  safeguarding boundary; must comply with BR-SG-02/05 and BR-NOTIFY-03 (which
  already exempts officer safeguarding notifications from the notification
  budget, implying such notifications exist in the rule model).
- If C is chosen, the decision must explicitly answer:
  - **Who receives it?** (affected person / officer / named operational role)
  - **What information is disclosed?** (fact of restriction only? kind? reason?)
  - **When?** (imposition only? lifting too? reminder?)
  - **Through which channel?** (in-app / push / email; BR-NOTIFY rules apply)
  - **What happens if delivery fails?** (retry, fallback channel, silent skip)

### Decision record

```
Decision ID: S-2
Owner: product owner
Additional required stakeholder: safeguarding lead
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 3. S-3 — Coordinator awareness of restrictions

**Current behavior:** coordinators see operational **effects only** — a
restricted candidate disappears from matching, assignment attempts fail like any
other ineligibility. No coordinator DTO mentions safeguarding. Architecture
tests (`SafeguardingIsolationTests`, `SafeguardingLeakSweepTests`) fail the
build if safeguarding types leak into other modules.

### Decision — choose one:

**Option A. Keep current silent behavior.**
- Privacy impact: none. Safeguarding impact: none (BR-SG-02/05 fully honoured).
- Operational impact: coordinators cannot distinguish a restriction from an
  ordinary ineligibility.
- API/UI implications: none. ADR required: no.

**Option B. Provide a neutral operational eligibility/status indicator.**
- Privacy impact: low-to-moderate — the *fact* "handled by safeguarding" becomes
  visible to coordinators without case details.
- Safeguarding impact: bounded but nonzero; case data itself stays closed.
- Operational impact: coordinators can stop re-offering to ineligible people.
- API/UI implications: a new coordinator-facing field/flag in eligibility or
  roster surfaces (designed only after you choose — not designed here).
- ADR required: yes, a small one (it touches BR-SG-05's non-disclosure surface).

**Option C. Provide more detailed coordinator visibility.**
- Privacy impact: high — safeguarding facts reach non-officers.
- Safeguarding impact: **contradicts BR-SG-02 as written**; would require
  amending the rule.
- Operational impact: fullest awareness.
- API/UI implications: coordinator DTOs would carry safeguarding-derived data;
  isolation tests would need a deliberate, documented change.
- ADR required: yes (amending BR-SG-02 is a decision, not a fix).

### Decision record

```
Decision ID: S-3
Owner: product owner
Additional required stakeholder: safeguarding lead
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 4. S-4 — Organization-less safeguarding case ownership

**Current behavior:** organization-attached cases are strictly tenant-scoped
(an officer of org B gets 404 for an org-A case); **organization-less cases are
visible to every active officer of any organization** (today's single-org
behaviour); no speculative platform role exists. Source of the boundary and its
options: `docs/security/safeguarding-scoping-policy.md`.

> **S-4 must be resolved before a second organization is onboarded.**

### Decision — choose one (the repository's three defined options):

**Option A. PLATFORM_LEVEL ownership — keep platform-level until a second
organization is onboarded.**
- Owner: every active `SafeguardingOfficer` (of any organization) collectively.
- Authorization boundary: exactly the current `VisibleCasesAsync` rule
  (org-less → all officers).
- Org-less case lifecycle: unchanged.
- Audit requirements: unchanged (`SafeguardingAccessLog` per read/action).
- Multi-org implications: **becomes information sharing between organizations
  that never agreed to it** — this is the stated reason the decision exists.
- Operational responsibility: diffuse; no named accountable officer.
- Migration implications: none now.
- Failure mode: silent cross-organization exposure once a second org exists.

**Option B. ORGANIZATION_SCOPED ownership — introduce a platform officer
capability (e.g. `PlatformSafeguardingOfficer`) that exclusively owns org-less
cases.**
- Owner: holders of the new capability only.
- Authorization boundary: service-level scoping change + endpoint capability
  check; architecture tests added.
- Org-less case lifecycle: cases may need assignment/reassignment rules — must
  be specified in the decision (initial assignment path, unassignment, handover).
- Audit requirements: unchanged, plus assignment events.
- Multi-org implications: clean isolation; recommended by the scoping policy
  document for multi-org (that wording is the repository's, not this pack's
  recommendation).
- Operational responsibility: explicit and nameable.
- Migration implications: new capability + data review of existing org-less
  cases; no schema change expected.
- Failure mode: org-less cases with no capable officer until one is appointed.

**Option C. Require an organization on every case.**
- Owner: whichever organization is attached.
- Authorization boundary: simplest (pure tenant scope).
- Org-less case lifecycle: **org-less cases become impossible**.
- Audit requirements: unchanged.
- Multi-org implications: clean, but contradicts **ADR-007** (organization-
  optional platform) and BR-TENANT-02's org-optional model — the scoping policy
  document itself marks this option "not recommended" (repository wording).
- Operational responsibility: per-org officers only.
- Migration implications: existing org-less cases would need an organization or
  another home — undefined without further policy.
- Failure mode: neighbourly concerns raised outside any organization have no
  home (ADR-007 conflict).

### Decision record

```
Decision ID: S-4
Owner: product owner
Additional required stakeholder: technical lead (safeguarding lead advisory)
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 5. B-1 — Block + existing assignment

**Current behavior:** `BlockUserAsync` is idempotent, stores a reason, sends no
notification, **does not unassign**. Existing assignments stay
`Assigned`/`InProgress`. Protections already active and test-pinned: discovery
hidden, location withheld, contact/visit address withheld, matching filtered,
future offers blocked both ways, existing messages remain readable (BR-COMM-02).

**The only open question:** *What should happen to an existing assignment when
one participant blocks the other?*

### Decision — choose one:

**Option A. Keep the assignment active; communication/location remain blocked.**
- Changes: none to behaviour; the rule text records the policy.
- Operational impact: the assignment may complete, but the blocked pair cannot
  exchange address/contact/location; coordinators see an assignment that cannot
  progress normally (rescheduling runs through them).

**Option B. Automatically unassign.**
- Changes (later phase): block action triggers unassignment of the affected
  assignment with status transition, audit entries, and a replacement path.
- Must be specified with the choice: what the counterpart sees (no block
  disclosure), whether the request returns to the open pool, notification
  behaviour (only if separately approved), race handling (block racing an
  assignment change — transactional, idempotent).

**Option C. Create an explicit coordinator review workflow.**
- Changes: a flagged-assignment queue for coordinators to decide per case.
- **Requires a subsequent design decision** (queue semantics, coordinator
  permissions vs S-3 outcome, disclosure boundaries) — mark C as
  "requires its own design step" if chosen.

### Decision record

```
Decision ID: B-1
Owner: product owner
Additional required stakeholder: organization coordinators (operations input)
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 6. B-2 — Unsafe-behavior report → officer surface

**Current behavior:** an `UnsafeBehavior` community report is marked
`EscalatedToSafeguarding` **on the report row only**. No `SafeguardingCase` is
created, no officer is notified. Visibility is via the ordinary moderation /
report surface.

### Decision — choose one:

**Option A. Manual officer conversion into a safeguarding case.**
- Trigger: an officer, reviewing reports, deliberately creates a case from one.
- Owner: officers. Duplicate handling: officer judgement at conversion time.
- Severity: set by the officer. Audit trail: case creation + access log.
- Tenant boundary: case takes the report's tenant context (or org-less per S-4).
- Officer visibility: reports surface must expose the escalation flag to
  officers only.
- User notification implications: none automatic.
- Implementation complexity: lowest (a guarded "create case from report" action).

**Option B. Automatic safeguarding case creation.**
- Trigger: every `UnsafeBehavior` report.
- Owner: system creates; officers triage.
- Duplicate handling: needs a defined rule (per reporter/target/time window) —
  must be specified with the choice.
- Severity: default classification needed — must be specified.
- Audit trail: automatic creation events.
- Tenant boundary: derived from report; interacts with S-4 for org-less.
- Officer visibility: cases appear without officer action (noise risk — the
  matrix notes retention/noise cost).
- User notification implications: none automatic.
- Implementation complexity: medium (dedup, defaults, retention).

**Option C. Digest/queue-based officer escalation.**
- Trigger: periodic or count-based digest of escalated reports to officers.
- Owner: product defines cadence; officers act on the digest.
- Duplicate handling: digest aggregates duplicates by design.
- Severity: orderable within the digest — must be specified.
- Audit trail: digest dispatch recorded.
- Tenant boundary: digest scope per organization (and org-less per S-4) —
  must be specified.
- Officer visibility: officer-only channel; nothing user-facing.
- User notification implications: none automatic.
- Implementation complexity: medium (scheduling + scoping).

### Decision record

```
Decision ID: B-2
Owner: product owner
Additional required stakeholder: safeguarding lead
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 7. ADR-026 — Visit address sharing (product + legal)

**Current state:** ADR-026 is **Proposed**; no option selected; **no
VisitAddressGrant artifact exists in the code** (verified); the Phase 4 gate is
intact — address/phone/coordinates reach only the **live** assignee
(`Assigned`/`InProgress`), are withheld on block or `CONTACT_BLOCKED`/
`LOCATION_BLOCKED` restriction, and access ends with
cancellation/completion/no-show/expiry; `LocationSharingConsent` is a separate
standing setting and is not used as address consent; auditability unchanged.

### Decision — choose one of the three existing options (no fourth option exists):

**Option A. Implicit sharing under the defined assignment rules.**
- Repository facts: matches BR-COMM-04 as written; no new UI step; home visits
  keep working. The consent is implicit and unrelated to the Phase 3 setting; a
  family member can enter the address (BR-HELP-04), so the senior never agrees
  personally; it cannot be revoked before the visit.
- Implementation impact if chosen: none (current behaviour is already this);
  Flutter explanation text already describes it.

**Option B. `PRECISE` location consent requirement.**
- Repository facts: one setting, one mental model; but it merges two separate
  permissions. Every existing senior is at consent level `NONE` (Phase 3
  migrated nobody), so every home visit silently loses its address on
  deployment; to restore visits, seniors would be pushed to share exact
  coordinates for discovery too — the opposite of data minimisation.
- Implementation impact if chosen: gate change binding address disclosure to
  the Phase 3 consent + migration/communication implications.

**Option C. Dedicated per-assignment visit-address grant.**
- Repository facts: purpose-bound, one visit, one volunteer, revocable,
  independent of discovery settings; aligns with GDPR purpose
  limitation/minimisation *as generally understood* (no legal claim is made
  here). Costs: a new step in the time-critical assignment flow; a
  non-answering senior blocks the visit; needs a coordinator path for
  offline/IVR seniors; changes BR-COMM-04's wording.
- **Option C is design-prepared but NOT approved and NOT implemented.**
  Its plan lives in `docs/decisions/ADR-026-implementation-plan.md`
  (data model, lifecycle, integration) and architecture tests currently fail
  the build if it is implemented before approval. If you choose C, the decision
  must define: grant issuer, recipient, assignment binding, expiration,
  revocation, audit event, tenant scope, API contract, UI behaviour, and
  failure behaviour — the implementation plan is the starting point, not a
  substitute for your specification.

### The four unresolved legal questions (reproduced verbatim from the ADR — do not answer here)

1. Is the address disclosure under BR-COMM-04 covered by a legal basis other
   than consent? (If yes, Option A plus good transparency may suffice.)
2. May a family member with `CanCreateHelpRequests` grant the visit-address
   consent on the senior's behalf? May a coordinator, for seniors without the
   app?
3. What happens when the senior does not answer before the visit: reminder,
   coordinator call, or automatic unassignment?
4. Should a coordinator ever see the visit address, e.g. to send a replacement
   volunteer? If yes, under which audited reason?

**Legal counsel / product owner decision required.** Until a follow-up revision
of ADR-026 records the choice and the answers, BR-COMM-04 stays in force
unchanged.

### Decision record

```
Decision ID: ADR-026
Owner: product owner
Additional required stakeholder: legal counsel (external legal review)
Selected option: PENDING          (A | B | C)
Decision date:
Rationale:
Constraints:
Conditions:
Implementation authorization:
Documentation authorization:
```

---

## 8. Implementation readiness matrix

| Decision | Current state | Owner decision needed | Implementation impact | Security impact | Legal impact | Gate |
|---|---|---|---|---|---|---|
| ADR-027 (V-3) | Proposed; computed ladder live; tests pin separation | Ratify (A) or order alignment (B) | A: docs-only · B: dedicated phase | None in both | None | none (independent) |
| S-2 | No notification, no task; effects immediate | Choose notification model | B/C: new task/notification path | Must not leak case facts | None | none today |
| S-3 | Coordinators see effects only | Choose awareness level | B: new coordinator-facing flag · C: rule amendment | Disclosure boundary (BR-SG-02/05) | None | none today |
| S-4 | Org-less cases visible to all officers | Choose ownership model | B: new capability + scoping change | Directly a safeguarding-architecture decision | None | **hard gate before 2nd organization** |
| B-1 | Block does not unassign; protections active | Choose assignment fate | B: unassignment flow · C: review workflow (own design) | None (protections hold in all options) | None | none today |
| B-2 | Escalation flag only; no officer surface | Choose escalation model | A: guarded conversion action · B: auto-create · C: digest | Officer-surface design must respect BR-SG-01/02 | None | none today |
| ADR-026 | Proposed; no grant artifact; live-assignment gate intact | Choose A/B/C + 4 legal answers | C: new table + gate + UI (plan ready) | Withholding/protections hold in all options | **Legal determination required** | feeds Legal Gate |

---

## 9. Legal Gate — NOT PASSED

Repository-established requirements, none of which is currently verified:

- [ ] Legal entity established
- [ ] Lawyer / external legal review of privacy policy + terms
- [ ] DPA requirements in place with every operating organization
- [ ] EU hosting verified contractually
- [ ] Insurance answers **in writing**: P0-01 (coordinator call), P0-02
      (insurance broker), P0-08 (budget calendar)
- [ ] ADR-026's four legal questions answered
- [ ] Processing register (Verarbeitungsverzeichnis) and DPIA confirmed current
      for the operating deployment

No GDPR compliance, legal sufficiency, insurance coverage, or lawful basis is
claimed anywhere in this pack.

## 10. Early Access gate

Documented gate (from `docs/product/early-access-validation-plan.md` — not
started, not executed):

- [ ] Willing community / early-access participants (none contacted; none assumed)
- [ ] Legal Gate passed (§9)
- [ ] Explicit **written** risk acceptance of still-open decisions by the
      operating organization
- [ ] Safeguarding readiness: a named safeguarding lead holding
      `SafeguardingOfficer`; S-4 resolved if more than one organization will
      ever exist on the deployment
- [ ] Operational ownership named (operator, on-call, data-protection contact)

The plan is organization-neutral: no named partner, no pilot dependency, no
invented participants.

---

*End of pack. Fill each record's fields, then hand the pack to the next
implementation phase. Human approval is the final authority.*
