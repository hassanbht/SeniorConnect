---
name: SeniorConnect-trust-safety
description: Use when touching verification, trust levels, capabilities, safety levels, matching eligibility, safeguarding, or anything that decides who is allowed to do what in SeniorConnect. Highest-risk area of the codebase.
---

# SeniorConnect Trust & Safety

> This is the highest-risk area of the product. If you are unsure, stop and ask rather
> than guessing. A wrong decision here can put a vulnerable person in a room with the
> wrong person.

## The three separate concepts

```
Identity   — is this person who they claim to be?
Trust      — how much has been verified about them, by whom, and until when?
Capability — what may they do RIGHT NOW?
```

Never merge them. Never derive one from another implicitly.

## Hard rules

```
1. Trust level is computed server-side from verification records. Never stored as
   client-writable. Never sent by the client.
2. Safety level is determined server-side by IActivitySafetyPolicy from the category
   AND the full context. The client sends the category, never the level.
3. Matching invariant:
      volunteer.effectiveTrustLevel >= request.requiredTrustLevel
      AND volunteer has the required capability
      AND organization policy allows
   All three, server-side. A failing candidate is ABSENT, not low-ranked.
4. The platform never claims a person is safe. It states what was verified, by whom, when.
5. No verification documents are stored. Outcomes only.
6. Verifications expire. Expiry lowers the trust level and flags affected assignments
   for a human — it never silently auto-cancels.
7. The buddy rule (first three Level-3+ activities) is enforced at matching AND
   re-checked at assignment. It cannot be bypassed via a direct API call.
8. Safeguarding is a separate schema, separate DbContext, separate policy.
   OrganizationAdmin does NOT imply safeguarding access.
9. Every safeguarding read is logged.
10. Blocked categories (nursing, medication, paid work) return a REFERRAL, never a request.
11. Emergency phrasing routes to 144/112, never into the request queue.
12. AI proposes, humans decide. No AI may auto-assign a Safety-Level-3+ activity.
```

## When adding or changing a category

```
[ ] Is it non-medical, non-nursing, unpaid?  If no → blocked list + referral group
[ ] What is its default safety level?
[ ] Under which contexts does the level change? (location, vulnerability, medication)
[ ] Which trust level and which trainings does that imply?
[ ] Does it require organization approval?
[ ] What is the insurance context?
[ ] Write the test cases for every branch of the policy
```

## When changing trust computation

```
[ ] Recompute is deterministic and side-effect free
[ ] A snapshot is written to trust_level_snapshots
[ ] Users whose level DROPS are notified with a plain-language explanation
[ ] Affected future assignments are flagged for a coordinator
[ ] The /me/trust endpoint's `missing[]` array is updated so the user knows the next step
```

## When touching safeguarding

```
[ ] Is the data still invisible to search, exports, reports, dashboards, notifications?
[ ] Is the read logged?
[ ] Is the reporter's identity still hidden from the subject?
[ ] Is the note store still append-only?
[ ] Does an OrganizationAdmin without the capability still get 403? (test it)
```

## Copy review

Every user-facing string in this area must pass:

```
✗ "Verified — safe"          ✓ "Identität am 12.05.2026 bestätigt"
✗ "100 % sicher"             ✓ "Von Caritas Tirol bestätigt"
✗ "Hilfe ist unterwegs"      ✓ "Rufen Sie 144 an"  + the button
✗ "Zugriff verweigert"       ✓ "Dafür fehlt noch: Strafregisterbescheinigung. So geht's: …"
```
