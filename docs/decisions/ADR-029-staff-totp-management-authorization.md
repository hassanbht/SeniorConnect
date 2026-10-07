# ADR-029 — Staff TOTP Management Authorization

- Status: **Accepted** (implemented; transcribed 2026-10-07 from in-code record)
- Scope: who may manage staff two-factor (TOTP) security

## Problem

TOTP enrollment endpoints must be restricted to staff, but "staff" was at
risk of being derived from how a user signed in. Password is legitimate for
seniors and ordinary volunteers, so authentication method must never imply
staff privilege.

## Decision

Staff authorization is based on **active Coordinator/Admin membership** in
any organization. Not authoritative: authentication method, TOTP presence,
client flags.

## Architecture

```text
Identity (authentication boundary)
   |  authenticated user id only
   v
IStaffSecurityAuthorizer (Organizations.Contracts — Contracts-only reference)
   |  active Coordinator/Admin membership?
   v
canManageStaffSecurity (server-computed, default false)
```

- `Identity` references only `Organizations.Contracts`, never Domain /
  Application / Infrastructure (enforced by `ModuleBoundaryTests`).
- The authorizer is an optional dependency of `IdentityService` and fails
  closed when absent or unreadable.
- Ordinary `Volunteer`/`Staff` roles, invited/suspended/left memberships,
  unknown users, and platform-only safeguarding roles resolve to `false`.
- Which tenants a staff member may act on is enforced separately by the
  calling endpoint via `organizationId`.

## Endpoints

- `POST /api/v1/me/totp/enroll` and `POST /api/v1/me/totp/confirm` are both
  gated (confirm must not bypass the enrollment gate). No disable/reset
  endpoints exist.
- No TOTP secret, OTP, QR payload, or recovery material is logged; denial
  logs record only the user id.

## Tests

- `StaffTotpAuthorizationTests` (11 cases: grant + full denial matrix).
- `IdentityServiceIntegrationTests` (enrollment, confirmation, login with
  TOTP, already-enabled conflict).
- Flutter `wave2_client_boundary_test` (no auth-method staff inference).

## Alternatives considered

- Deriving staff-ness from authentication method: rejected (password is
  valid for non-staff).
- Direct `Identity -> Organizations` dependency: rejected (module boundary).
- Client-submitted capability flags: rejected (never trust the client).
