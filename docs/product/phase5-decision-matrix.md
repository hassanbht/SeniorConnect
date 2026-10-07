# Phase 5 Decision Matrix (5A + 5B)

> Status: 2026-09-28 (Phase 5B). This file records what the repository
> **actually** does today for each open item, what the business rules say, what
> the tests pin, and what remains open. It does **not** invent product or legal
> decisions.
>
> Classification used in Phase 5B (brief §2):
>
> - **A — ALREADY DETERMINED BY AN EXISTING BUSINESS RULE.** Code and tests are
>   aligned to the rule. Closed here.
> - **B — TECHNICAL INTERPRETATION OF AN EXISTING RULE.** Implementation
>   detail; no product input needed. Closed here.
> - **C — PRODUCT DECISION REQUIRED.** Not decided by the agent.
> - **D — LEGAL/COMPLIANCE DECISION REQUIRED.** Not decided by the agent.
> - **E — IMPLEMENTATION BUG.** Fixed here if one is found.
>
> Status vocabulary: **RULE DETERMINED** (the rule answers it), **DECISION
> REQUIRED** (a named owner must decide), **RESOLVED**, **PENDING**,
> **BUG — FIX REQUIRED**. Sources of truth: `docs/product/business-rules.md`,
> the security documents under `docs/security/`, the code and the tests.

---

## 0. Decision inventory reconciliation (Phase 5B §1)

The Phase 5A final report said **"eleven pending decisions"**, but its own
status block listed eleven *rows*, one of which (`V-4`) was already
**RESOLVED**. The count was therefore off by one.

Canonical inventory:

| Bucket | Count | Items |
| --- | --- | --- |
| Tracked items | **11** | the 10 decision IDs (`V-1…V-4`, `S-1…S-4`, `B-1`, `B-2`) **plus** the visit-address item (ADR-026) |
| Pending after 5A | **10** | ADR-026, `V-1`, `V-2`, `V-3`, `S-1…S-4`, `B-1`, `B-2` |
| Resolved after 5A | **1** | `V-4` |

**There is no eleventh open decision.** The word "eleven" described the
tracked rows, not the open ones. This file and
`docs/security/phase5-security-gate.md` now say **10 pending of 11 tracked**.
No new decision ID was invented.

---

## 1. Classification and outcome

| ID | Topic | Class | Outcome in 5B |
| --- | --- | --- | --- |
| V-1 | Minimum discovery verification level | **A** | **RESOLVED** — BR-DISC-03 states it; code + tests match |
| V-2 | Organization roster without the discoverable switch | **A** | **RESOLVED** — BR-DISC-03 second sentence states it |
| V-3 | `TrustLevelCalculator` vs BR-TRUST-02 | **B** + C | **RESOLVED 2026-09-30** — ADR-027 ratified (Option A); separation made explicit + tested |
| V-4 | Who may record address-verification facts | **A** | **RESOLVED** — BR-DISC-04; re-verified in 5B |
| S-1 | Officers and location without consent | **A** | **RESOLVED** — BR-DISC-06 answers it |
| S-2 | Notification/workflow for restrictions | **C** | **RESOLVED 2026-09-30** — Option B approved & implemented (content-free operational task) |
| S-3 | Whether coordinators learn of restrictions | **C** | **RESOLVED 2026-09-30** — Option B approved & implemented (neutral indicator) |
| S-4 | Org-less cases / platform-level officer | **C** | **RESOLVED 2026-09-30** — Option B approved & implemented (`PlatformSafeguardingOfficer`); tenant scope for org cases enforced |
| B-1 | Block vs. existing assignment | **A** + C | **RESOLVED 2026-09-30** — Option B approved & implemented (auto-unassign on block, idempotent) |
| B-2 | UnsafeBehavior auto case creation | **C** | **RESOLVED 2026-09-30** — Option A approved & implemented (manual officer conversion, no auto-creation) |
| ADR-026 | Visit-address sharing | **D** | **RESOLVED 2026-09-30 as product decision** — Option C approved & implemented, feature-gated OFF; legal validation PENDING before production activation |

No item was classified **E**: the Phase 5B review found no implementation bug
against an existing rule. Everything the brief listed as a rule-determined
safety improvement was already in place; the evidence is in §12 of this file
and in `docs/security/phase5-security-gate.md`. *(The S-2/S-3/S-4/B-1/B-2/
ADR-026 rows above were later resolved by the owner decision pack of
2026-09-30 and implemented — see §2 and the individual sections.)*

---

## V-1 — Minimum discovery verification level

- **Class:** A · **Status:** RESOLVED (RULE DETERMINED) · **Blocking:** no.
- **Source of truth:** BR-DISC-03 sentence 1 — "Open discovery lists only people
  who are active, discoverable, and contact-verified (email + phone) or
  organization-verified".
- **Current implementation:** `DiscoveryEligibilityService.MinimumSelfVerification
  = VerificationLevel.EmailPhone`; open discovery additionally requires the
  person's own `discoverable = true` and an active account
  (`backend/modules/Profiles/Infrastructure/DiscoveryServices.cs`). Address
  verification is not required to be listed.
- **Operation matrix (as implemented).** No new risk category was invented; only
  categories the repository actually has appear.

  | Operation | Required verification | Organization requirement | Discoverable requirement | Safeguarding requirement |
  | --- | --- | --- | --- | --- |
  | Open discovery of people | EMAIL+PHONE (or organization-verified) | none | yes (own switch) | no active DISCOVERY restriction, no block |
  | Matching candidates for a request | same | active roster of the request's org may substitute for the discoverable switch | no, if on that org's roster | + no active ASSIGNMENT restriction |
  | Owned content (a person's own open requests) | none | none | no | no DISCOVERY restriction, no block |
  | Higher-risk help requests | listing is unchanged; BR-SAFETY-02/03 gate **assignment** by required trust level | per category | n/a | as above |

- **Current tests:** `Phase4VerificationDiscoveryTests`
  (`T10_T11`, `T14`, `T15`); new `Phase5PolicyClosureTests` pins the
  surrounding boundaries.
- **Required action:** none. The `(decision V-1)` marker in BR-DISC-03 is a
  product confirmation annotation, not a behaviour gap; removing it is a
  one-line docs edit whenever product confirms. **No behaviour is pending.**
- **Owner:** product — for the marker only. **Date:** 2026-09-28.

## V-2 — Organization roster membership without the discoverable setting

- **Class:** A · **Status:** RESOLVED (RULE DETERMINED) · **Blocking:** no.
- **Source of truth:** BR-DISC-03 sentence 2 — "Organization matching may also
  use the organization's own active roster".
- **Current implementation:** an active volunteer of the request's own
  organization is a candidate for **that organization's** matching without the
  public discoverable switch (`DiscoveryEligibilityService.FilterEligibleAsync`;
  the roster is consulted only when `Purpose == Matching` and an
  `OrganizationId` is present). With `Purpose == OpenDiscovery` the roster set is
  empty, so roster membership never leaks into public discovery.
- **Current tests:** `T15_ScenarioB_AnOrganizationVolunteer_FollowsOrganizationRules`
  and `Matching_UsesDiscoverableVerifiedVolunteers_ForCommunityRequests` pin both
  directions.
- **Required action:** none. Tenant isolation, organization authorization,
  account-active state and safeguarding restrictions all continue to apply to
  roster candidates (the same filters run for every purpose).
- **Owner:** product — for the marker only. **Date:** 2026-09-28.

## V-3 — TrustLevelCalculator vs BR-TRUST-02

- **Class:** B (technical interpretation) · **Status:** RESOLVED (ADR-027 ratified 2026-09-30, Option A) · **Blocking:** no (no disclosure path reads the number).
- **Source of truth:** BR-TRUST-01/02/03, ADR-027 (Accepted 2026-09-30).
- **Current implementation:** the computed ladder is 1 = phone **or** email,
  2 = both, 3 = + address, 4 = + identity, 5 = + background check
  (`TrustLevelCalculator`). BR-TRUST-02's table is an assurance ladder
  (1 = contact, 2 = identity, 3 = address, 4 = organization, 5 = safety).
- **The two concepts are separate and are now explicit in code and tests.**
  - `VerificationFacts` (the discovery fact set) carries no trust or safety
    level; `TrustLevelEvaluation` carries no verification level
    (`Phase5PolicySweepTests`).
  - Organization verification is a **fact**, never a trust tier: recording it
    leaves the computed level unchanged (`Phase5PolicyClosureTests.V3_*`).
  - Identity verification moves the trust ladder but not the discovery ladder
    (`Phase5PolicyClosureTests.V3_*`).
  - No numeric "trust score" and no rating-to-verification conversion exists.
- **Resolved:** ADR-027 ratified 2026-09-30 (Option A) — computed ladder is
  platform-normative; BR-TRUST-02 re-worded. (Original action text: keep the
  computed ladder as normative, reword BR-TRUST-02) **or** commission the
  code-alignment phase. **No production behaviour was changed in 5B.**
- **Owner:** product owner + tech lead. **Date:** 2026-09-28.

## V-4 — Who may record address-verification facts

- **Class:** A · **Status:** RESOLVED (re-verified in 5B) · **Blocking:** no.
- **Source of truth:** BR-DISC-04 — "Address and organization facts are recorded
  only by staff of the person's organization, with an explicit method."
- **Current implementation:** `VerificationService.RecordAsync` requires
  `IOrganizationAccessGuard.EnsureStaffAsync(actorUserId, organizationId)` for
  *that* organization, plus active roster membership of the subject. Platform
  admins have no path (fail closed). Provenance stored per fact: subject,
  verifier, organization, timestamp, method, validity, revocation
  (`Verification.RecordManual`). Geocoding success is never verification.
- **Current tests:** `T03`, `T06` in `Phase4VerificationDiscoveryTests`
  (self-verification forbidden, cross-tenant 404, method allow-list, no rows
  written on failure).
- **Required action:** none. Re-open only if a platform-level verifier flow is
  requested (would need a new capability and a BR-DISC-04 amendment).
- **Owner:** product + legal if widened. **Date:** 2026-09-28.

## S-1 — Safeguarding officers and location access without consent

- **Class:** A · **Status:** RESOLVED (RULE DETERMINED) · **Blocking:** no.
- **Source of truth:** BR-DISC-06 — "A restriction only withholds; no role,
  including safeguarding officers, bypasses location consent."
- **Current implementation:** `LocationVisibilityService.DecideManyAsync` applies
  blocks and restrictions as overrides **toward `None`**; there is no officer
  branch. `LocationVisibilityPolicy.Decide(OrganizationStaff, …)` caps staff at
  `Approximate` and never reaches `Precise`.
- **Current tests:** `T28`, `T18_T19_T29`, `T05_T06_T07_T12_T13`; new
  `Phase5PolicyClosureTests.S1_NeitherStaffNorOfficerRole_LiftsLocationConsent`
  (with `PRECISE` consent, a `LOCATION_BLOCKED` restriction still yields `None`
  for the officer).
- **Required action:** none. An **emergency officer exception would be a new
  feature**, not a pending decision — it requires a new ADR plus a documented
  legal basis (e.g. GDPR Art. 6(1)(d)) before any implementation.
- **Owner:** product + legal if an exception is ever proposed.
  **Date:** 2026-09-28.

## S-2 — Notification/workflow for automatic restrictions

- **Class:** C · **Status:** **RESOLVED 2026-09-30 (Option B, approved &
  implemented)** — when a restriction is imposed, a content-free operational
  task (`operational_tasks`, safeguarding schema) is created for the coordinator
  surfaces. It carries no safeguarding reason, no restriction type and no case
  reference (BR-SG-05); completion/cancellation are explicit; duplicate tasks
  per (organization, subject, open) are prevented by index. See
  `TrustSafety.Domain.OperationalTask`, `SafeguardingOperationalReader`
  (Contracts) and the leak-sweep tests. · **Blocking:** no.
- **Source of truth:** BR-SG rules define case handling (raise, assign, notes,
  close, audit, `SafeguardingAccessLog` per action). **No rule** defines
  notifying officers or creating coordinator tasks when a restriction is
  imposed. BR-NOTIFY-03 exempts "a safeguarding notification to an officer"
  from the notification budget — implying such notifications exist — but does
  not say when they are sent.
- **Current implementation:** restrictions are imposed/lifted by an officer
  through the API; every action writes a `SafeguardingAccessLog` (BR-SG-06).
  Restrictions never auto-create cases; `SafeguardingRestriction.Impose`
  refuses resolved/closed cases.
- **Implemented (2026-09-30):** imposing a restriction on an organization-scoped
  case creates one content-free `OperationalTask` (safeguarding schema,
  `SAFEGUARDING_FOLLOW_UP` kind — no reason, no restriction kind, no case
  reference) for the case's organization; lifting the restriction cancels the
  open tasks; duplicates per (organization, subject, open) are prevented
  (single open-task check + `ix_operational_tasks_open_per_subject`). Org-less
  platform cases create no task. `SafeguardingOperationalReader` (Contracts)
  is the only cross-module read surface.
- **Current tests:** `SafeguardingIsolationTests`,
  `SafeguardingLeakSweepTests` (architecture), `Phase5PolicyClosureTests` S-2
  authorized-recipient / unauthorized-staff / cross-tenant / no-leak /
  idempotency coverage.
- **Required action:** none — decided and implemented.
- **Owner:** product + safeguarding lead (decided 2026-09-30).

## S-3 — Whether coordinators should know about restrictions

- **Class:** C · **Status:** **RESOLVED 2026-09-30 (Option B, approved &
  implemented)** — coordinators receive only a neutral, content-free
  operational indication ("unavailable for operational reasons") through
  `ISafeguardingOperationalReader` (TrustSafety Contracts). No restriction kind,
  reason, case reference, officer identity or safeguarding DTO type crosses the
  boundary; the architecture leak-sweep keeps it that way. · **Blocking:** no.
- **Source of truth:** BR-SG-02/05 — case data never reaches non-officers;
  coordinators are not officers; no rule describes what a coordinator may learn.
- **Current implementation:** coordinators observe **effects**, not the
  restriction: a restricted candidate is filtered out of matching, and
  assignment attempts fail like any other ineligibility. No coordinator DTO
  mentions safeguarding.
- **Implemented (2026-09-30):** in addition, coordinators receive a neutral,
  content-free operational indication ("unavailable for operational reasons")
  exclusively via `ISafeguardingOperationalReader` — the DTO crosses module
  boundaries as a TrustSafety **Contract**, carries no restriction kind,
  reason, case reference or officer identity, and the architecture leak-sweep
  fails the build if a safeguarding type crosses elsewhere.
- **Current tests:** `SafeguardingIsolationTests`, `SafeguardingLeakSweepTests`
  (architecture) assert safeguarding types never leak into other modules;
  `Phase5PolicyClosureTests` cover allowed coordinator access, ordinary-staff
  denial and no-leak assertions.
- **Required action:** none — decided and implemented.
- **Owner:** product + safeguarding lead (decided 2026-09-30).

## S-4 — Organization-less cases and a platform-level officer role

- **Class:** C (the tenant-scope half is A and enforced) · **Status:**
  **RESOLVED 2026-09-30 (Option B, approved & implemented)** — org-less cases
  are PLATFORM_LEVEL and are visible only to the explicit
  `PlatformSafeguardingOfficer` role (POST/DELETE platform-officer endpoints;
  ordinary org officers no longer see org-less cases implicitly). Organization
  cases remain strictly tenant-scoped; all access stays audited. Assignment/
  removal lifecycle is defined in
  `docs/security/safeguarding-scoping-policy.md`. · **Blocking:** resolved
  before a second organization joins (hard requirement met).
- **Source of truth:** BR-SG-02 (explicit `SafeguardingOfficer` capability) and
  ADR-007 (the platform is organization-optional). **No rule** says who handles
  cases with `OrganizationId == null`.
- **Current implementation:** `SafeguardingService.VisibleCasesAsync` (S-4
  rule, implemented 2026-09-30): organization-attached cases of the caller's
  officer organizations, **org-less cases only for the explicit
  `PlatformSafeguardingOfficer` role** (platform-officer POST/DELETE endpoints),
  plus cases assigned to the caller. Organization cases are strictly
  tenant-scoped (an officer of another organization gets 404). An ordinary
  organization officer no longer sees org-less cases implicitly.
- **Current tests:** `TrustSafetyAuthorizationTests`
  (`Officer_lists_only_own_organisation_cases`),
  `Phase5PolicyClosureTests.S4_*` (tenant scope, platform-officer-only
  org-less access, cross-tenant restriction refusal).
- **Policy boundary:** the `PLATFORM_LEVEL` vs `ORGANIZATION_SCOPED` distinction
  is written down in `docs/security/safeguarding-scoping-policy.md` (§3/§5:
  appointment, removal, lifecycle, emergency fail-closed behavior).
- **Required action:** none — decided and implemented; the hard requirement
  (resolve before a second organization joins) is met.
- **Owner:** product + tech lead (decided 2026-09-30).

## B-1 — Block action: existing assignments, proposals, conversations

- **Class:** A for the effects, C for unassignment · **Status:** **RESOLVED
  2026-09-30 (Option B, approved & implemented)** — a newly created block
  releases every live assignment between the pair (`ReleaseAssignmentForSafety`:
  status → Open, assignee cleared, audited status-history row whose reason names
  the safety cause without identifying the counterpart). Deterministic,
  idempotent, runs only on first block creation (BR-DISC-05). Implemented via
  `IAssignmentBlockEnforcementPort` (TrustSafety Contracts) implemented by the
  HelpRequests module. · **Blocking:** no.
- **Source of truth:** BR-DISC-05 (a block hides both people in discovery,
  request lists and location, and withholds BR-COMM-04 contact; only the blocker
  can lift it; who blocked whom is never shown). ADR-026 hardening extends the
  withholding to a live assignment. **No rule orders unassignment.**
- **Current implementation:** `BlockUserAsync` is idempotent, stores a reason,
  notifies nobody. Effects flow from readers: discovery
  (`SafetyBoundaryReader.GetBlockedUserIdsAsync`), location
  (`LocationVisibilityService` → `None`), contact/address
  (`HelpRequestService.IsContactWithheldAsync`), matching (candidates filtered).
  Existing messages stay readable (BR-COMM-02); future offers between the pair
  are impossible in both directions.
- **Implemented (2026-09-30):** on **first** creation of a block,
  `IAssignmentBlockEnforcementPort` (TrustSafety Contracts, implemented by the
  HelpRequests module) releases every live assignment between the pair:
  `ReleaseAssignmentForSafety` moves Assigned/InProgress → **Open**, clears the
  assignee and timestamps, bumps RowVersion and writes an audited status-history
  row whose reason names the safety cause without identifying the counterpart.
  Idempotent (repeated blocks create no second history row); no block-reason
  notification is sent (BR-DISC-05).
- **Current tests:** `T12_T23`, `T24`, `T24_T37`, `T30`;
  `Phase5PolicyClosureTests.B1_A_Block_ReleasesTheLiveAssignment_BackToOpen`,
  `B1_A_Block_IsIdempotent_AndLeavesNoResidueOnRepeat`, and the
  block-then-request-vanishes-for-released-volunteer assertion.
- **Required action:** none — decided and implemented. **The security requirement
  is met in every state.**
- **Owner:** product + organization coordinators (decided 2026-09-30).

## B-2 — UnsafeBehavior reports and automatic case creation

- **Class:** C · **Status:** **RESOLVED 2026-09-30 (Option A, approved &
  implemented)** — UnsafeBehavior reports remain reports; escalation stays a
  flag. A safeguarding officer may manually convert an escalated
  unsafe-behavior, member-targeted report into a safeguarding case
  (`POST /api/v1/community/reports/{id}:convert-to-case`); the report keeps a
  traceability link (`SafeguardingCaseId`, officer, timestamp); duplicate
  conversion is a 409; severity is officer-set; no automatic creation, no
  notification. The conversion crosses the boundary via the
  `ISafeguardingCaseRaiser` port (Contracts only). · **Blocking:** no.
- **Source of truth:** BR-SG-01 — safeguarding is a separate subsystem, **not**
  an extension of ordinary reporting. Nothing mandates automatic case creation
  from a community report.
- **Current implementation:** `CommunityService.FileReportAsync` marks
  `CommunityReportCategory.UnsafeBehavior` reports as
  `EscalatedToSafeguarding` **on the report row** and creates no
  `SafeguardingCase` and no officer notification. The flag's only consumer is
  the moderation queue / report listing.
- **Implemented (2026-09-30):** a safeguarding officer may **manually** convert
  an escalated, member-targeted unsafe-behavior report into a safeguarding case
  (`POST /api/v1/community/reports/{id}:convert-to-case`). Severity is
  officer-set (fail-closed parse); the report keeps
  `SafeguardingCaseId`/`ConvertedByOfficerUserId`/`ConvertedAtUtc` traceability;
  duplicate conversion is a conflict; no automatic creation, no notification.
  The Community→TrustSafety call crosses the boundary via the
  `ISafeguardingCaseRaiser` **port** (Contracts only).
- **Current tests:** `Phase4VenueAndReportTests.T25_T26`;
  `tests/backend/SeniorConnect.Modules.Community.Tests/B2ReportToSafeguardingCaseConversionTests.cs`
  (officer converts, non-officer → Forbidden, duplicate → conflict, only
  escalated member-targeted reports convertible, item-target refused).
- **Required action:** none — decided and implemented.
- **Owner:** product + safeguarding lead (decided 2026-09-30).

## Visit-address sharing — ADR-026

- **Class:** D · **Status:** **RESOLVED 2026-09-30 as a PRODUCT decision
  (Option C, approved & implemented) — legal validation PENDING before
  production activation.** Implemented behind the `VisitAddress:RequireGrant`
  feature gate, which ships **false**: an architecture guard fails the build if
  any committed configuration enables it. Grant/revoke endpoints, service gate,
  senior-side Flutter prompt (de/en/fa) and the `visit_address_grants` table
  exist; BR-COMM-04 stays in force until an operator enables the gate after the
  Legal/Data Protection Gate. No legal basis or compliance is claimed.
- **Source of truth:** BR-COMM-04 (binding, unchanged) + ADR-026 (Proposed).
  Four legal questions remain open (legal basis other than consent; family /
  coordinator consent on the senior's behalf; no-answer path; coordinator
  access to the address).
- **Current implementation (ADR-026 Option C, approved & implemented
  2026-09-30, feature-gated OFF):** the assigned volunteer of a **live**
  assignment (`Assigned`, `InProgress`) receives the typed `LocationAddress`
  **only while a non-revoked `VisitAddressGrant` exists** for the request
  (`VisitAddress:RequireGrant` gate; ships `false` and is
  architecture-guarded against enabling in any committed configuration —
  BR-COMM-04 stays in force until an operator enables it after the Legal Gate).
  The senior (or the creator) grants/revokes from the Flutter request-status
  screen (de/en/fa). A block or a `CONTACT_BLOCKED`/`LOCATION_BLOCKED`
  restriction withholds it; cancellation/completion/no-show end the access;
  staff never receive it through these endpoints; geocoding only stores the
  caller's own coordinates. Grant storage carries **no address data**.
- **Current tests:** `T34`, `T35`, `T36_T38`, `T24_T37`, `T37`;
  `Phase5PolicyClosureTests.VisitAddress_*` (gate off → address hidden; gate on
  → only live grant releases; grant gated + block → released volunteer loses
  the request) and `Phase5PolicySweepTests`
  (`VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration`,
  `VisitAddressGrant_StoresNoAddressData`).
- **Required action:** none for engineering — decided and implemented,
  activation-gated. The four open legal questions in ADR-026 (legal basis
  beyond consent; family/coordinator consent on the senior's behalf; no-answer
  path; coordinator access) remain with legal review; production activation
  additionally requires the Legal/Data Protection Gate in writing.
- **Owner:** product (decided); **legal validation pending**.

---

## 2. Summary

| ID | Topic | Class | Status | Owner |
| --- | --- | --- | --- | --- |
| V-1 | Discovery minimum verification | A | RESOLVED (RULE DETERMINED) | product (marker only) |
| V-2 | Roster without discoverable switch | A | RESOLVED (RULE DETERMINED) | product (marker only) |
| V-3 | TrustLevelCalculator vs BR-TRUST-02 | B | RESOLVED (ADR-027 ratified 2026-09-30, Option A) | — |
| V-4 | Who records address facts | A | RESOLVED | — |
| S-1 | Officer location without consent | A | RESOLVED (RULE DETERMINED) | — |
| S-2 | Restriction notifications/workflow | C | **RESOLVED 2026-09-30 (Option B, implemented)** | product + safeguarding lead (decided) |
| S-3 | Coordinator awareness | C | **RESOLVED 2026-09-30 (Option B, implemented)** | product + safeguarding lead (decided) |
| S-4 | Org-less cases / platform officer | C | **RESOLVED 2026-09-30 (Option B, implemented)** | product + tech lead (decided) |
| B-1 | Block vs. existing assignment | A + C | **RESOLVED 2026-09-30 (Option B, implemented: auto-unassign)** | product + organization coordinators (decided) |
| B-2 | UnsafeBehavior auto case | C | **RESOLVED 2026-09-30 (Option A, implemented: manual officer conversion)** | product + safeguarding lead (decided) |
| — | Visit address (ADR-026) | D | **RESOLVED as product decision 2026-09-30 (Option C, implemented, feature-gated) — legal validation PENDING** | product (decided); legal review pending |

**Pending decisions: 0.** The owner decision pack (2026-09-30) resolved every
tracked item: ADR-027 = A, S-2 = B, S-3 = B, S-4 = B, B-1 = B, B-2 = A,
ADR-026 = C. All seven are implemented and tested. The only open gate is
external: ADR-026's production activation requires the Legal/Data Protection
Gate (`VisitAddress:RequireGrant` ships false; the four legal questions in
ADR-026 remain with legal review).
**Resolved in 5B: 11 of 11** (V-1…V-4, S-1…S-4, B-1, B-2, ADR-026-as-product).
