# MVP Scope Definition & Architectural Boundaries

> **Status:** Frozen as of Phase 9 / Production Release.
> **Scope:** Phases 1–4 core mutual-aid loop + Phase 5 Community + Phase 6 Family Delegation + Phase 7 Hardening & Compliance + Phase 8 Scale & Intelligence + Phase 9 Production Pilot Readiness.

---

## 1. Core Mission & Invariants

SeniorConnect is a trusted local platform where seniors, families, volunteers, and municipal organizations organise everyday help and shared activities.

- **Non-Negotiable Invariant 1**: The platform MUST provide value when no social organization or municipality is present.
- **Non-Negotiable Invariant 2**: Safety before convenience. Trust level, safety level, and authorization are computed **server-side only**.
- **Non-Negotiable Invariant 3**: Two-tier GDPR data isolation. Safeguarding data lives in an isolated PostgreSQL schema (`safeguarding`) and is never accessible to regular staff or exportable into reports.
- **Non-Negotiable Invariant 4**: Accessibility is architecture. Full support for Senior Mode (64dp touch targets), 3 languages (`de`, `en`, `fa`), LTR and RTL layouts, high contrast 7:1, and text scaling up to 2.0x without clipping or overflow.

---

## 2. In-Scope Modules (MVP & Phase 1–9)

| Module | Core Capabilities | Bound Business Rules |
| :--- | :--- | :--- |
| **Identity & Auth** | Passwordless SMS OTP, Email Magic Link, Staff TOTP, Device Revocation, Session Management | `BR-AUTH-01..07` |
| **Profiles & Taxonomy** | Senior Mode, 11 categories (including generalized scope), Reference Skills & Languages | `BR-SCOPE-01..05` |
| **Help Requests & Matching** | Picture-card request flow, Hybrid matching, Buddy rule for Level 3+, Optimistic Concurrency (`xmin`), Idempotency | `BR-HELP-01..08`, `BR-SAFETY-01..06` |
| **Trust & Safety** | Levels 0–5, Criminal record verification, Key custody custody audit, Expense settlement | `BR-TRUST-01..07`, `BR-KEYS-01..05` |
| **Safeguarding** | 2-tap concern reporting, Isolated DbContext/schema, Officer assignment workflow, Access audit logging | `BR-SG-01..06` |
| **Coordinator & Reporting** | Volunteer roster search, Silent volunteer flags, Expiring verification alerts, XLSX spreadsheet export | `BR-ROSTER-01..03`, `BR-REPORT-01..04` |
| **Community** | Groups, Join policies, Events with recurrence & capacity, Contextual message threads with moderation | `BR-COMM-01..05` |
| **Family & Delegation** | Granular permissions, Printed Zugangskarte (6-digit QR), Trusted contacts, Non-emergency Safety Alerts | `BR-FAMILY-01..06` |
| **Privacy & GDPR** | DPIA, DPA template, Art. 20 Data export, Two-tier deletion, Retention cleanup jobs | `BR-GDPR-01..05` |

---

## 3. Out-of-Scope (Permanently Parked)

- Payment gateways & commercial care fee transactions (SeniorConnect is for mutual aid and volunteer solidarity, not commercial care mediation).
- Public 5-star ratings of human beings (strictly prohibited; only factual, non-evaluative trust badges are permitted).
- Third-party analytics, behavioral tracking, or advertising SDKs.
- Background GPS location tracking.
- Password fields on senior-facing or volunteer-facing mobile screens.
