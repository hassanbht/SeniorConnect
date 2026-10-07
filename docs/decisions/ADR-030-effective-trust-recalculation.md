# ADR-030 — Effective Trust Recalculation

- Status: **Accepted** (implemented; transcribed 2026-10-07 from in-code record)
- Scope: WHEN the trust value is (re)calculated. Does NOT change the
  approved ADR-027 ladder (0–5, deterministic, no numeric score).

## Problem

A login-time trust snapshot without an invalidation rule is a cache with an
unknown TTL: a verification revoked or expired after login kept authorizing
until the next login.

## Decision

Effective trust is calculated **from live verification facts at each
trust-consuming boundary** (`ITrustLevelReader`, consumed by HelpRequests,
Matching, Community). The login-time `TrustLevelSnapshot` row remains as
audit evidence but no authorization path reads it as authority.

Recalculate on each trust-consuming read/operation — NOT on every HTTP request.

## Rules

- Revocation or expiry of a verification changes effective trust immediately.
- Missing or deactivated accounts resolve to trust 0.
- The client can never submit an authoritative TrustLevel
  (`EndpointSecurityTests.No_inbound_request_carries_a_server_owned_value`).
- Safeguarding restrictions, ratings, and organization verification remain
  separate inputs; they never silently become identity verification.

## Tests

- `TrustLevelReaderRecalculationTests` (7 cases: live-facts read,
  revocation, expiry, deactivation, unknown user, stale snapshot ignored,
  batch mix).
- `TrustLevelCalculatorTests` / `TrustLevelComputationTests` (ladder).

## Alternatives considered

- Snapshot-as-authority with TTL: rejected (unknown-TTL cache).
- Recalculation on every HTTP request: rejected (cost without benefit;
  trust is authoritative when read/calculated at consuming boundaries).
- Persisting a separate stale authoritative level: rejected (no ADR
  requires it).
