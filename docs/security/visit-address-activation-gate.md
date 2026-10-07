# Visit-Address Activation Gate (ADR-026)

> **Status:** 2026-10-01 (deployment-integration phase: gate re-verified —
> committed configs unchanged at `false`, architecture guard green in the full
> run, HTTP dry run confirmed gate-off behavior with zero grant rows; state
> remains **ENGINEERING IMPLEMENTED / ACTIVATION OFF**). Originally 2026-09-30
> (launch-readiness phase). This document is an
> **activation-control record**, not an authorization. It separates what the
> repository **implements** from what an operator must complete **externally**
> before the feature may be switched on for a deployment.
>
> **Engineering implementation ≠ Production activation authorization.**

---

## 1. Engineering state (implemented, verified)

ADR-026 Option C is fully implemented and tested:

- `VisitAddressGrant` entity + `visit_address_grants` table (public schema,
  unique filtered live-grant index `ux_visit_address_grants_live`).
  **No address data is stored in the grant** — only the consent decision
  (guarded by `VisitAddressGrant_StoresNoAddressData`).
- Grant/revoke endpoints `POST|DELETE /api/v1/help-requests/{id}/visit-address-grant`
  (senior/creator-side; Flutter card, de/en/fa).
- Address release to the live assignee (`Assigned`/`InProgress`) happens only
  while a non-revoked grant exists — and only when the feature gate is on.
- Migration `20260930103345_ApprovedPolicyImplementationS2S4B1B2Adr026`
  verified against a clean PostgreSQL database (empty DB → full chain →
  schema/index/constraint check).

## 2. Gate state (verified 2026-09-30; re-verified 2026-10-01)

| Check | Result |
| --- | --- |
| `VisitAddress:RequireGrant` in committed configs | `false` (only `appsettings.json` defines the section) |
| Environment defaults turning it ON | none (no `appsettings.*.json` overrides the section) |
| Flutter client can activate it | no — the flag is server configuration, not an API input |
| Ordinary API callers can activate it | no — no endpoint writes configuration |
| Accidental committed activation | prevented by architecture guard `VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration` (fails the test run if any committed config sets `"RequireGrant": true`) — green in the 2026-10-01 full run (59/59 architecture, 612 total) |
| Runtime behavior with the gate OFF (HTTP dry run, 2026-10-01) | live assignee sees the address under the unchanged BR-COMM-04 baseline, `visit_address_grants` holds **zero** rows for the synthetic assignment, exact address withheld from organization staff — 4/4 checks PASS |

**Phase state: ENGINEERING IMPLEMENTED / ACTIVATION OFF.**

## 3. Invariants that hold in BOTH gate states

These are independent of the gate and never weaken:

- BR-COMM-04 withholding for blocks and `CONTACT_BLOCKED`/`LOCATION_BLOCKED`
  restrictions (a grant never overrides a block/restriction).
- Live-assignment-only access; cancellation/completion/no-show end access.
- Staff roles never receive the address through these endpoints.
- Officers never bypass location consent (BR-DISC-06).
- With the gate **off**, no exact visit address is exposed through the grant
  path at all (default behavior unchanged from Phase 4 hardening).

## 4. Activation procedure (deployment-level, external)

Setting `VisitAddress:RequireGrant: true` for a deployment requires, **in
this order, all in writing**:

1. **Legal/Data Protection Gate §2 (BUILD-CHECKLIST) signed** by the
   accountable legal owner for this deployment, including written answers to
   the four open ADR-026 questions: (a) legal basis for the visit-address
   disclosure beyond consent; (b) representative/family authorization to
   grant on the senior's behalf; (c) no-response behavior; (d) coordinator
   access to the address. **Requires qualified external review.**
2. **Activation record** naming: deployment, date, approver, gate checklist
   version, and the person flipping the flag.
3. **Configuration change** through the operator's change procedure
   (environment/secret store — not a repository commit).
4. **Post-activation verification**: grant/revoke round-trip in a staging
   environment with synthetic data; confirm withholding still applies under
   block/restriction.

Until step 1–4 are complete for a deployment, the gate stays `false`. No
repository artifact may set it to `true`.

## 5. Explicit non-claims

This document does not claim legal sufficiency, lawful basis, GDPR
compliance, or any legal approval. It records an engineering control and the
external conditions under which an operator may activate it. Legal review is
**pending**.
