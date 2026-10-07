# ADR-027 — Trust-level definition (BR-TRUST-02 vs TrustLevelCalculator)

Status: **Accepted (Option A — computed ladder ratified, 2026-09-30)**
Date: 2026-09-28 · Ratified: 2026-09-30 (owner decision, see
`docs/decisions/OWNER-DECISION-PACK.md` and
`docs/decisions/APPROVED-POLICY-IMPLEMENTATION-REPORT.md`)
Relates to: BR-TRUST-01/02/03/07, BR-SAFETY-02/03, BR-AUTH-08/10, ADR-003, ADR-013,
ADR-021, `docs/product/phase5-decision-matrix.md` (V-3)

## Context

The repository contains two different 0–5 trust ladders.

**BR-TRUST-02 (business-rules.md, binding table)** — an assured-identity ladder:

| Level | Name | Achieved by |
| --- | --- | --- |
| 0 | Registered | account created |
| 1 | Contact Verified | email **and** phone verified |
| 2 | Identity Verified | identity confirmed via an approved provider or manual staff review |
| 3 | Address Verified | address confirmed (Meldebestätigung, postal code letter, or org confirmation) |
| 4 | Organization Verified | an organization vouches for this person |
| 5 | Safety Verified | background check **and** training **and** org approval |

**`TrustLevelCalculator.Evaluate` (Identity module)** — the ladder the running
system computes:

1. **1** — phone **or** email verified
2. **2** — phone **and** email verified
3. **3** — address verified
4. **4** — identity verified
5. **5** — background check verified

Organization verification and training are not part of the computed ladder.
Every auth/refresh flow stores the computed level (`TrustLevelSnapshot`), and
matching consumes it via `ITrustLevelReader` against `RequiredTrustLevel`
(BR-SAFETY-03); `CapabilityService` derives `PerformSafetyLevel1..5`
capabilities from it (P4-04 forbids hand-granting those).

Phase 4 left the calculator unchanged on purpose: matching outcomes depend on
the numeric values, and the discrepancy note in business-rules.md marked the
divergence as decision V-3.

### A. Is the code wrong?

Measured against BR-TRUST-02 as written — yes in three places: level 1
requires only one contact channel; identity is level 4 instead of 2; address
is level 3 instead of higher; organization verification is missing entirely.
The implementation also *cannot* represent BR-TRUST-02 level 5 (needs training
records) or level 4 (needs the organization fact the Identity module only
reads through `VerificationFactsReader` since Phase 4).

### B. Is BR-TRUST-02 outdated?

Partly. It predates ADR-016/ADR-021's reality that email and phone are
independently verifiable and that many legitimate users will have exactly one
verified channel (BR-AUTH-10 restricts unverified-*email* accounts without
demanding a phone). Its level 5 also depends on training records the platform
does not yet model. Its *names* (Registered → Contact → Identity → Address →
Organization → Safety) describe an assurance story product still wants.

### C. Are they describing different concepts?

Yes. The rule table is an **assurance ladder** (how strongly is this person
identified), while the calculator produces a **capability gate input** whose
only consumers are `PerformSafetyLevelN` capability derivation and the
`RequiredTrustLevel` threshold in matching. Nothing in the disclosure layer
(location, contact, discovery eligibility) reads the number — those decisions
rest on consent, blocks and restrictions (BR-DISC-02), which is why the
divergence has caused no security incident.

## Decision

**RATIFIED 2026-09-30 — Option A.** The owner approved ratifying the computed
ladder as the platform-normative definition. The original proposal text below is
kept for the historical record; its direction is now the decision:

1. **For the platform, the computed ladder is the normative definition** of the
   0–5 trust level. BR-TRUST-02's table is reworded to describe the computed
   ladder (see the proposed wording below) and the old table is kept under
   "Alternatives".
2. **Organization-verified and training remain separate facts** (ADR-003
   separation; BR-DISC-04 set-of-facts model). If product wants them ranked
   into the ladder, a future ADR does it explicitly, with a data migration of
   `RequiredTrustLevel` thresholds.
3. **No numeric "trust score"** beyond the level is introduced. Trust
   (computed), ratings (ADR-009: none public), safety levels (per-activity,
   `IActivitySafetyPolicy`) and organization verification (a fact) stay
   conceptually separate, as BR-TRUST-01 requires.

**BR-TRUST-02 wording (APPLIED 2026-09-30 per ratification):**

| Level | Name | Achieved by |
| --- | --- | --- |
| 0 | Registered | account created |
| 1 | Contact Reachable | email **or** phone verified |
| 2 | Contact Verified | email **and** phone verified |
| 3 | Address Verified | address confirmed by org staff (explicit method) |
| 4 | Identity Verified | identity confirmed via an approved provider or manual staff review |
| 5 | Safety Verified | background check recorded (training and org approval remain separate facts) |

If product rejects this direction, the alternative is aligning the code to the
original table, which requires: capability remapping, snapshot backfill,
matching-threshold review, and tests — estimated as its own phase, not a gate
fix.

## Consequences

- business-rules.md keeps BR-TRUST-02 but its Phase 4 divergence note now
  points here; the note also states the computed ladder is what matching and
  capabilities consume today.
- Matching and capability behavior is unchanged by this ADR (zero code
  change).
- The conceptual separation ADR-003 requires is preserved; verification,
  trust, ratings, safety and organization verification remain distinct
  domains.
- When the decision lands: either apply the proposed wording (docs-only) or
  schedule the code alignment phase with the migration and threshold work.

## Alternatives considered

1. **Original BR-TRUST-02 table (align code to rule).** Rejected for the
   platform: silent level drops for EMAIL-only users, destabilised matching
   thresholds, and level 5 is unrepresentable without training records.
2. **Keep code, delete the rule table.** Rejected: the table encodes the
   product's assurance vocabulary; it should be corrected, not erased.
3. **Hybrid: change only level 1 to "email and phone".** Rejected: it is the
   change with the largest user impact (demotes EMAIL-only volunteers) and the
   least benefit, since nothing gates on level 1 alone.
