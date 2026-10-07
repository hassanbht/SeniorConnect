# Early-Access Readiness Pack

> **Update 2026-10-02 (Release Candidate Freeze & External Activation Evidence).**
> Re-verified this phase: the pack below is complete as a *readiness statement*
> and **nothing in it was delivered to anybody**. **Participant status remains
> NOT STARTED** — no participant, senior, family, volunteer, community, partner
> or second organization was named, contacted, recruited or onboarded, and no
> real participant data was used. Scope is **FROZEN** per
> `docs/product/controlled-early-access-scope.md`. `VisitAddress:RequireGrant`
> remains `false`. No launch was authorized; the launch decision remains the
> owner's alone.
>
> **Status:** 2026-10-02 (Production Activation Gate & Controlled Early Access

> **Status:** 2026-10-02 (Production Activation Gate & Controlled Early Access
> Readiness phase). Companion to `docs/product/early-access-validation-plan.md`
> (P9-08). This pack states what must be **in place** before the operating
> owner may decide to launch — it does **not** start the program and does
> **not** authorize a launch (see §29 GO/NO-GO rule of the launch-readiness
> phase brief: the launch decision belongs to the operating owner, never the
> agent).
>
> **Companion documents for this phase:**
> - `docs/launch/PRODUCTION-ACTIVATION-GATE.md` — 12 gates A–L with full
>   evidence tables (repository-proven vs. external dependency)
> - `docs/launch/PRODUCTION-ACTIVATION-READINESS-REPORT.md` — §28 final report
> - `docs/product/controlled-early-access-scope.md` — implemented functionality only
> - `docs/product/controlled-early-access-stop-conditions.md` — actual policies, no invented KPIs

---

## 0. Readiness Summary (2026-10-02, production-activation phase)

This summary distinguishes **engineering readiness** (what the repository
proves) from **deployment, legal, operational, participant, and owner gates**
(what must be provided externally). Engineering completeness never implies
external approval.

### 0A. Engineering Dimensions (repository-proven)

| Dimension | State | Basis |
| --- | --- | --- |
| **Engineering** | READY | Release build 0 errors; 612/0/0 backend + 59/59 architecture tests with real PostgreSQL; HTTP synthetic dry run PASS (55/55); Docker image build PASS (249 MB chiseled, UID 1654); synthetic restore drill PASS |
| **Security** | READY | Security suites green; provider boundaries fail closed; tooling credential removed from working tree and tracked for external rotation (`docs/security/credential-exposure-remediation.md`); no SeniorConnect runtime secret committed; §24 secret scan: 0 actual secrets in working tree |
| **Safeguarding Engineering** | READY | S-2/S-3/S-4/B-1/B-2 implemented and verified end-to-end over HTTP (55/55); S-4 PostgreSQL constraint defect fixed and re-verified; appointments themselves remain operational actions (below) |

### 0B. Production Activation Gate Summary (A–L)

The full evidence tables live in
`docs/launch/PRODUCTION-ACTIVATION-GATE.md`. High-level summary:

| Gate | Scope | Status |
| --- | --- | --- |
| **A. Credential Security** | Rotation of 3 tooling keys | PENDING — EXTERNAL ACTION |
| **B. Authentication Provider** | Real SMTP provider contract | PENDING — EXTERNAL |
| **C. Production Infrastructure** | Host, PostgreSQL, DNS, TLS, registry | BLOCKED — NOT STARTED |
| **D. Database** | Migration chain | PASS (engineering) / PENDING (real deployment) |
| **E. Backup** | Real backup target | BLOCKED — NOT PROVISIONED |
| **F. Observability** | Monitoring/logging/alerting | PENDING — EXTERNAL |
| **G. Operational Ownership** | 8 roles | PENDING — EXTERNAL APPOINTMENT |
| **H. Safeguarding Ownership** | Lead, Platform Officer, Coordinator | PASS (engineering) / PENDING (appointments) |
| **I. Data Protection / Legal** | Qualified legal review (10 items) | PENDING — EXTERNAL REVIEW |
| **J. ADR-026** | Visit-address sharing | OFF (engineering implemented; activation OFF) |
| **K. Participant Readiness** | Recruitment / onboarding | NOT STARTED |
| **L. Launch-Owner Authorization** | Early Access launch decision | PENDING |

### 0C. Participant & Owner Dimensions

| Dimension | State | Basis |
| --- | --- | --- |
| **Participants** | NOT STARTED | No participant, partner, or second organization named, contacted, or committed |
| **Owner launch decision** | PENDING | Belongs exclusively to the operating owner |

No success percentages or KPIs are defined here by design.

---

## 1. Participant Readiness

- **Participant recruitment source: NOT YET SELECTED.** No organization,
  community, volunteer, senior, or family is named, contacted, or committed.
  Any future recruitment follows ADR-028 (partners are possibilities, never
  dependencies) and the organization-neutrality of the validation plan.
- **Consent / onboarding flow:** implemented — informed consent at
  registration; versioned consent management with withdraw (BR-GDPR-01/02,
  P7-05); discoverability is an explicit per-person switch (BR-DISC-02);
  location sharing is explicit consent with `NONE/APPROXIMATE/PRECISE`
  levels (BR-GEO-05). Onboarding routine per
  `docs/operations/pilot-onboarding-playbook.md`.
- **Support contact:** to be named by the operating organization at launch
  (role placeholder: [Operating Organization Support Contact]). The platform
  does not provide human support by itself.
- **Reporting path:** in-app report/block flows (community reports; blocks
  with 404-never-confirm semantics); safeguarding concerns
  (`POST /api/v1/safeguarding/concerns`) for any authenticated user.
- **Emergency disclaimer:** the platform is **not** an emergency service, not
  a medical service and not an acute-care provider. The in-app emergency
  screen routes to 144/112 (Austrian emergency numbers) and states this
  boundary; participants must receive the same statement during onboarding.
- **Data-use transparency:** public-facing privacy documentation
  (`docs/legal/privacy-policy.md`, Verarbeitungsverzeichnis) describes
  categories, purposes, and retention; implementation terms match
  (location consent, discoverability, verification, trust, safeguarding,
  blocking, reporting, visit-address grant — see §10 of the brief). No
  compliance is claimed; deployment-specific validation is required.

## 2. Feature Scope

Only currently implemented functionality is in scope. The full controlled
Early Access scope is enumerated in
[`docs/product/controlled-early-access-scope.md`](controlled-early-access-scope.md).
Summary:

- Phase 2 coordinator wedge, Phase 3 help & matching, Phase 4 trust &
  safeguarding, Phase 5 community; deployment hardening (P9).
- The seven approved policy behaviors: ratified trust ladder (ADR-027),
  content-free operational task (S-2), neutral coordinator indicator (S-3),
  platform safeguarding officer (S-4), auto-unassign on block (B-1), manual
  officer report conversion (B-2), visit-address grant **feature-gated OFF**
  (ADR-026).
- Explicitly **out of scope**: announcements/events beyond what an
  organization enables, AI auto-assignment at Safety Level 3+ (ADR-014
  boundary), payments/mediation (permanently parked), voice/IVR, corporate
  volunteering, white-labeling, visit-address sharing (ADR-026 OFF), and any
  future roadmap feature.

## 3. Safety

| Control | State at launch (required) |
| --- | --- |
| Community safeguarding lead | appointed per participating community (BR-SG-02) |
| Platform safeguarding officer (S-4) | appointed **before** launch; org-less cases are platform-officer-only, fail-closed |
| Escalation | concern → officer queue → case lifecycle (open/assign/note/close), every action audited |
| Reporting | community reports; unsafe-behavior flags escalate; officer-only manual conversion (B-2) |
| Block | idempotent, discovery/location/contact withholding, live assignment auto-released (B-1), who-blocked-whom never shown |
| Assignment termination | cancel/complete/no-show end all protected-data access; block releases the assignment |
| Emergency boundary | 144/112 routing in-app; platform states it is not an emergency service (README/onboarding wording to be included by the operator at launch) |
| Visit-address sharing | gate OFF; activation only per `docs/security/visit-address-activation-gate.md` |

## 4. Stop Conditions

Based on actual repository policy (no invented KPIs). The full stop-conditions
document is at
[`docs/product/controlled-early-access-stop-conditions.md`](controlled-early-access-stop-conditions.md).
The operating organization must stop a community's participation when any of
these occurs, until resolved:

1. **Any cross-tenant or safeguarding isolation defect is observed** —
   BR-TENANT-03 / BR-SG-02 violations are stop-the-line conditions
   (regression gate §15 of the previous phase).
2. **Withholding protections fail** — a block, restriction, cancellation, or
   revocation does not withhold BR-COMM-04/BR-GEO data.
3. **`ADR-026` gate is found enabled without the written activation record**
   (`docs/security/visit-address-activation-gate.md` §4) — immediate stop +
   gate reset to false.
4. **Legal/Data Protection readiness lapses** — DPA terminated, consent
   basis withdrawn, or Legal Gate §2 items no longer valid for the
   deployment.
5. **Safeguarding coverage lost** — no reachable safeguarding lead for the
   community or no reachable platform safeguarding officer for org-less
   cases (fail-closed means cases stop being worked; operational stop until
   re-appointment).
6. **Audit integrity failure** — access logs or status history can be
   altered/deleted (BR-SG-06 / BR-AUDIT-01 broken).
7. The **keep/change/stop** review of the validation plan (§6) recommends
   stop for that community.

Stop is reversible only when the underlying condition is demonstrably fixed
and re-verified (tests re-run; gate re-audited where applicable).

## 5. Program Boundary

- Early Access remains **NOT STARTED** as of this document.
- No second organization is onboarded as part early access preparation;
  multi-organization operation is a separate readiness state
  (S-4 requirement is met in engineering; appointment is operational).
- The launch decision itself: operating owner only, after
  `docs/launch/LAUNCH-READINESS-REPORT.md` §10 ("Next Gate") conditions are
  satisfied, and after
  `docs/launch/PRODUCTION-ACTIVATION-READINESS-REPORT.md` §17 (Launch Owner)
  records authorization.
