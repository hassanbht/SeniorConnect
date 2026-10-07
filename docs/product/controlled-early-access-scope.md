# Controlled Early Access — Scope

> **FROZEN — 2026-10-02 (Release Candidate Freeze & External Activation
> Evidence).**
>
> The scope below is **frozen**. No feature is added, removed or altered during
> this phase, and **no feature addition is permitted** until the owner lifts the
> freeze in writing. Re-verified this phase against the actual candidate:
> `VisitAddress:RequireGrant` is `false`, the ADR-026 architecture guard is
> green (59/59), and the Release build plus the 612-test backend suite pass with
> zero failures. Early Access remains **NOT STARTED**.
>
> **Date:** 2026-10-02 · **Phase:** Production Activation & Controlled Early

> **Date:** 2026-10-02 · **Phase:** Production Activation & Controlled Early
> Access Readiness
>
> This document defines the **only** functionality that is implemented,
> tested, and validated as ready for controlled real-world usage during Early
> Access. It contains **no future features** and introduces **no new
> engineering work**. Early Access is **NOT STARTED** — this is the scope
> definition that will be handed to the operating owner for the launch
> decision.
>
> Hard rules: no AI matching, no payments, no marketplace, no emergency/medical
> functionality, no voice/IVR, no corporate volunteering, no white-label, no
> unimplemented features.

---

## In Scope — Implemented & Validated

### Authentication & Identity

- Registration with email verification (magic link / OTP via SMTP adapter)
- JWT-based authentication with strict validation (`ClockSkew=0`)
- Consent-based onboarding with versioned consent management and withdrawal
  (BR-GDPR-01/02)
- Discoverability as an explicit per-person switch (BR-DISC-02)
- Location-sharing consent with `NONE / APPROXIMATE / PRECISE` levels
  (BR-GEO-05)
- Account deletion / data export endpoints (two-tier deletion)
- Rate limiting on authentication endpoints (auth 20/min, global 300/min)

### Profile

- User profile management
- Trust-level / trust ladder (ADR-027, ratified)

### Organisations & Memberships

- Organization scoping (server-side; cross-tenant reads answer 404)
- Role-based capabilities: Staff (0), Coordinator (1), Admin (2),
  SafeguardingOfficer (3), PlatformSafeguardOfficer (4), Volunteer (5),
  Client (6)
- Multi-organization boundary (engineering-verified; not activated during
  Early Access — single organization only)

### Discoverability

- Explicit per-person discoverability switch
- Snap-to-grid location privacy for matching
- Disclosure policy per relationship (staff/admin/coordinator/volunteer/client
  boundaries)
- BR-COMM-04 contact and location masking for blocks and restrictions

### Help Requests

- Coordinator wedge (Phase 2): create, view, assign, cancel, complete,
  no-show lifecycle
- Help request status history (append-only)
- Picture-card request flow
- Operational task (S-2): content-free task on restriction imposition
- Operational hold (S-3): neutral coordinator indicator

### Matching

- Coordinator-driven assignment
- Assignment acceptance / rejection flow

### Proposals

- Volunteer-to-request proposal flow
- Proposal acceptance / rejection by coordinator

### Assignments

- Live assignment to volunteer (status `Assigned` / `InProgress`)
- Assignment termination: cancel / complete / no-show ends all protected-data
  access
- Block → automatic assignment release (B-1): released volunteer receives 404
- Block → who-blocked-whom never shown

### Messaging

- In-app reporting path for concerns (`POST /api/v1/safeguarding/concerns`)
- Community reports and unsafe-behavior flags
- Block / report flows with 404-never-confirm semantics
- Append-only audit entries for all state transitions

### Community / Groups

- Community platform (Phase 5): groups, posts, reports, admin moderation
- Community reports to community admins
- Unsafe-behavior flags escalating to safeguarding officers

### Safeguarding Controls

- S-2 OperationalTask (content-free, leak-free) — verified over HTTP
- S-3 OperationalHold (content-free roster boolean) — verified over HTTP
- S-4 PlatformSafeguardingOfficer appointment via real audited API; org
  officers receive 404 (fail-closed) — verified over HTTP
- B-1 automatic assignment release on block — verified over HTTP
- B-2 manual officer report conversion with audit log — verified over HTTP
- Safeguarding isolation: case text never reaches `audit_entries`; separate
  `safeguarding` schema with access logs (BR-SG-06)
- Escalation: concern → officer queue → case lifecycle (open/assign/note/close)
- Audit integrity: append-only `audit_entries` interceptor; cannot be altered

### Operational Coordination

- Coordinator attention-queue triage
- Operational task follow-up flows
- Incident escalation path (7 incident types × 6 phases)

### Hours / Reporting

- Impact report export (P2-31) — available to organization coordinators
- Status history and completion metrics
- Audit trail export for safeguarding and operational review

### Emergency Boundary

- In-app emergency screen routing to 144/112 (Austrian emergency numbers)
- Platform explicitly states: NOT an emergency service, NOT a medical service,
  NOT an acute-care provider
- No emergency response dispatch or medical functionality

---

## Explicitly Out of Scope

The following are **not** part of Controlled Early Access and are **not**
activated during Early Access:

| Category | Excluded Items |
|---|---|
| **AI matching** | AI auto-assignment at Safety Level 3+ (ADR-014 boundary holds) |
| **Payments** | Payments, mediation (permanently parked) |
| **Marketplace** | No marketplace or commercial transactions |
| **Emergency / Medical** | No emergency dispatch, no medical functionality, no acute-care provision |
| **Voice / IVR** | No voice or IVR features |
| **Corporate volunteering** | Not included |
| **White-label** | No white-label branding |
| **Visit-address sharing** | ADR-026 feature-gated OFF; `VisitAddress:RequireGrant = false`; activation requires external written authorization |
| **Announcements / Events** | Only as configured by the participating organization; not a platform feature |
| **Future roadmap features** | Any feature not explicitly listed above |

---

## Engineering Validation

All in-scope functionality above is verified by:

- Backend regression: 612/0/0 (real PostgreSQL 16)
- Architecture tests: 59/59 (including ADR-026 feature-gate guard)
- Flutter tests: 906/906
- HTTP synthetic dry run: 55/55 (S-2/S-3/S-4/B-1/B-2, ADR-026 gate-off,
  audit verification, cross-tenant isolation, visit-address isolation)
- Synthetic restore drill: PASS (4 tables, 11,500 rows, SHA-256 `57f060…88ad0`)

See also: `docs/product/early-access-readiness-pack.md` §2 (Feature Scope)
