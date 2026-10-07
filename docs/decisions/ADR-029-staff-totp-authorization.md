# ADR-029 — Staff TOTP management authorization: authentication ≠ authorization

Status: Accepted
Date: 2026-10-05
Relates to: `docs/architecture/authorization.md`, `docs/architecture/trust-safety.md`,
`docs/product/business-rules.md` §BR-SEC-W1-09, `docs/security/security-overview.md`,
OWASP Authorization Cheat Sheet (server-side enforcement, default-deny)

## Context

Two-factor enrolment (`POST /me/totp/enroll`, `/me/totp/confirm`) was reachable
by **any authenticated user**: `EnrollTotpAsync` checked only that the account
existed and had no TOTP yet. Nothing asked whether the caller was staff.

The client worked around this by guessing staff-ness from the authentication
method:

```dart
isStaff: data.user.primaryAuthMethod == 'Password'
```

That inference is wrong in both directions. Password is a legitimate method for
seniors and ordinary volunteers (BR-AUTH-03), so a senior with a password was
offered the staff security tool. Conversely, a staff member signing in another
way would lose it. An authentication method is not a role, and TOTP presence is
not a privilege.

The server already exposes a decision field, `UserSummaryDto.CanManageStaffSecurity`,
currently hard-wired to `false` (fail-safe). This ADR closes the boundary:
who receives that capability, how it is resolved without breaking module
boundaries, and how the management endpoints are enforced.

## Decision

**"May manage staff two-factor" is a server-computed authorization, derived
from organization membership, never from the authentication method.**

1. **Capability definition.** Staff security management means enrolling,
   confirming, resetting or disabling TOTP for one's own staff account. It is
   granted to a user with an **active Coordinator or Admin membership in any
   organization**. Ordinary members, volunteers, seniors and unaffiliated users
   never receive it. Safeguarding-only appointments do not imply it.

2. **Resolution point.** `Identity` stays an authentication boundary. It never
   reads `Organizations` tables directly. An `Organizations`-owned Contracts
   port answers the single question:

   ```csharp
   // Organizations/Contracts/StaffSecurityContracts.cs
   public interface IStaffSecurityAuthorizer
   {
       Task<bool> CanManageStaffSecurityAsync(Guid userId, CancellationToken ct = default);
   }
   ```

   `IdentityService` consumes it as an **optional** constructor dependency
   (`IStaffSecurityAuthorizer? authorizer = null`) and fails closed when it is
   absent — the HelpRequests `IOrganizationAccessGuard?` precedent, not a
   required dependency (registration order: `AddIdentityModule` runs before
   `AddOrganizationsModule` in `Program.cs`).

3. **Surface.** `UserSummaryDto.CanManageStaffSecurity` carries the decision to
   the client as render-only information. The management endpoints enforce it
   independently with `CAPABILITY_MISSING`; UI hiding is never the enforcement.

4. **Fail-safe default.** Without the authorizer registered, or for any
   non-staff caller, the answer is `false`. A missing membership table, an
   unknown user, or an inactive membership all resolve to denial.

5. **No secrets in logs or responses beyond the enrolment itself.** The secret
   and provisioning URI are returned once at enrolment to the enrolling
   account and are never logged, never re-readable, and never visible to any
   other account.

## Alternatives considered

- **Derive from `PrimaryAuthMethod`.** Rejected: it is the defect this ADR
  closes. Auth method answers "how did they sign in", not "what may they do".
- **Required `IOrganizationAccessGuard` in `IdentityService`.** Rejected: it
  couples startup order and makes Identity depend on a coordinator-specific
  guard for a user-global question. The dedicated single-purpose port keeps
  the boundary narrow and the failure mode closed.
- **A new `ManageStaffSecurity` granted capability in `user_capabilities`.**
  Rejected for now: it would hand-grant a security privilege and needs a
  granting workflow, an approver and an audit trail that do not exist. The
  membership-derived rule needs none of those. A platform-admin escape hatch
  (`PlatformAdmin` ⇒ true) is a separate product decision, not part of this ADR.
- **Client-side gating only.** Rejected: OWASP default-deny; the server is
  always final.

## Security implications

- Closes an authorization bypass: TOTP enrolment for staff accounts is no
  longer reachable by arbitrary users, and seniors are no longer offered a
  staff control.
- Tenant-safe by construction: the authorizer checks membership anywhere, and
  enrolment always acts on the caller's own account (`userId` from the token,
  never from the body).
- Audit: enrolment, confirmation, denial and reset write a security-event log
  entry with user id, timestamp and outcome — never the secret, the code, the
  QR payload or a recovery code.

## Compatibility

- The client already reads `canManageStaffSecurity` (defaults to `false` on
  older servers), so enabling the server side needs no client change.
- No user is locked out: ordinary users simply never see the staff tool, and
  their own account security is unaffected.

## Tests

Positive: active coordinator/admin in any org ⇒ `true` and enrolment succeeds.
Negative: ordinary user, other-org irrelevance (membership is checked, org id
is never client-supplied), unaffiliated user, auth-method independence
(password user without membership ⇒ `false`; non-password staff ⇒ `true`),
flag cannot be submitted by the client, cross-account enrolment impossible.
Security: secret/code/QR never in logs, unauthorized reset/disable denied.

## Implementation status

Decided here; implemented in the same phase (Wave-3 §9–11): the Contracts port,
the Organizations implementation, the Identity consumption, the endpoint
enforcement, the audit entries and the test matrix. Until the implementation
lands, `CanManageStaffSecurity` remains `false` for everyone.
