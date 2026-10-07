# ADR-030 — Trust recalculation: effective trust is computed at trust-consuming boundaries

Status: Accepted
Date: 2026-10-05
Amends: nothing. Extends ADR-027 (ladder) and ADR-003 (trust model) with
recalculation semantics, which neither decided.
Relates to: `docs/architecture/authorization.md` §2–3, `docs/architecture/trust-safety.md`,
`docs/product/business-rules.md` §BR-TRUST-01–03, ADR-027, ADR-003

## Context

ADR-027 ratified the trust **ladder** (what each level means). It did not
decide **when** the value is recalculated. The implementation holds two
different answers:

- `BuildAuthResponseAsync` evaluates the ladder live from verification facts
  and writes a `TrustLevelSnapshot` — but only on login paths.
- `TrustLevelReader.GetEffectiveTrustLevelAsync` returns the latest snapshot,
  with no re-evaluation. A verification revoked or expired after login keeps
  authorizing until the next login.

The JWT carries the same login-time value with a 15-minute lifetime, and
`RefreshTokenAsync` already recomputes live without writing a snapshot. The
snapshot is therefore neither the single source of truth nor consistently
maintained: it is a login-time cache with no invalidation rule.

Meanwhile W2-5's `CapabilityService` reads verification facts live on every
resolution, so revocation already takes effect there. The inconsistency is the
defect: two trust consumers can disagree about the same person at the same
second.

## Decision

**Effective trust is computed from live verification facts at every
trust-consuming boundary. No persisted value is authoritative.**

1. **Trust-consuming boundaries** are exactly: capability resolution
   (`CapabilityService`), matching thresholds (`RequiredTrustLevel`),
   `PerformSafetyLevelN` derivation, assignment-time safety checks, and any
   trust-gated read. Each of them evaluates `TrustLevelCalculator` against the
   current facts on the way in.

2. **Authoritative inputs** are the verification table (`Verified` status,
   unexpired `ValidUntilUtc`), the account state (active, not suspended), and
   nothing else. The ladder itself is unchanged (ADR-027 Option A).

3. **Invalidation is immediate by construction.** There is nothing to
   invalidate: a revoked or expired fact stops counting on the next
   trust-consuming operation, because the operation reads the facts, not a
   cache. This is explicitly **not** "recalculate on every HTTP request" — a
   request that consumes no trust performs no calculation.

4. **`TrustLevelSnapshot` is demoted to an audit trail.** Login-time rows may
   remain as evidence of what was decided at login, but no authorization path
   may read them as authority. `TrustLevelReader` is repointed at live
   evaluation (or removed where the consumer already evaluates live).

5. **Separations are preserved (BR-TRUST-01).** Safeguarding restrictions,
   ratings, organization verification, training facts and background checks
   remain separate facts. Trust consumes them; it never merges with them. No
   numeric score is introduced.

6. **The client never supplies trust.** `TrustLevel`, `RequiredTrustLevel` and
   capabilities arriving from a client are rejected by the existing
   architecture test (`EndpointSecurityTests`).

## Alternatives considered

- **Recalculate on every HTTP request.** Rejected: wasteful and needlessly
  broad. Trust is consumed at a handful of boundaries; computing it there is
  sufficient and keeps the cost proportional to the decision.
- **TTL-based snapshot cache (e.g. 5 minutes).** Rejected: a TTL is still a
  staleness window during which a revoked volunteer keeps authorizing. The
  live read costs one indexed query and removes the window entirely.
- **Persist an authoritative `users.trust_level` column.** Rejected: it
  creates a second source of truth and an invalidation problem. The facts
  table is the truth; the level is derived.
- **Keep login-time snapshots as authority.** Rejected: this is the defect.
  Revocation must take effect before the next login.

## Security implications

- A revoked background check or expired address proof stops authorizing
  immediately, including for home visits — the highest-risk gate.
- Deactivated accounts resolve to no trust regardless of facts.
- No new data is stored; no migration is required. The snapshot table, if
  retained, is write-only evidence.

## Compatibility

- `AuthResponse.TrustLevel` and the JWT `trust_level` claim remain
  login-time values with a 15-minute lifetime; they are display/transport
  conveniences, not authority. Consumers must not treat them as decisions.
- No client change is required: the client never sent trust and continues
  not to.

## Tests

Unit: revocation/expiry between two consecutive trust-consuming operations
changes the outcome; deactivated account ⇒ no trust; each trust level boundary
of the ADR-027 ladder still holds. Integration: revoke a background check,
then attempt a Level-4 assignment without re-login ⇒ denied with
`TRUST_LEVEL_INSUFFICIENT`. Architecture: no authorization path reads
`TrustLevelSnapshots` as authority (test fails on the read).

## Implementation status

Decided here; implemented in the same phase (Wave-3 §12–13): repoint
`TrustLevelReader` at live evaluation, remove snapshot reads from
authorization paths, keep snapshot writes as audit evidence only, and pin the
behaviour with the test matrix above.
