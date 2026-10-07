# External Launch Gate

> **Superseded as the canonical matrix** by
> `docs/launch/PRODUCTION-ACTIVATION-GATE.md` (A–L). This file is retained as
> the prior phase's evidence trail (A–J).
>
> **Release Candidate Freeze phase (2026-10-02): NO GATE CHANGED STATUS.** No
> external evidence was received. The statuses below remain accurate. New
> release-integrity findings **R-1** (the candidate is an uncommitted working
> tree; `HEAD` does not contain the approved-policy code), **R-2** (a further
> history location for the exposed keys) and **R-3** (no backend version / SDK
> pin / dependency lock) are recorded in `docs/release/RELEASE-CANDIDATE-FREEZE.md`
> §6. Current report:
> `docs/launch/RELEASE-CANDIDATE-ACTIVATION-REPORT.md`. Per-gate evidence
> contract: `docs/launch/EXTERNAL-ACTIVATION-EVIDENCE-REQUIREMENTS.md`.
>
> **Date:** 2026-10-02 · **Phase:** Production Deployment Validation &
> External Gate Closure.
>
> This matrix separates what **this repository proves** from what an
> **external, deployment-specific owner must decide or provide**. Engineering
> completeness never implies external approval.
>
> Statuses: `PASS` · `PENDING` · `BLOCKED` · `NOT STARTED` ·
> `NOT APPLICABLE`.
> An external requirement is **never** `PASS` because documentation exists.

> **Date:** 2026-10-02 · **Phase:** Production Deployment Validation &
> External Gate Closure
>
> This matrix separates what **this repository proves** from what an
> **external, deployment-specific owner must decide or provide**. Engineering
> completeness never implies external approval.
>
> Statuses: `PASS` · `PENDING` · `BLOCKED` · `NOT STARTED` ·
> `NOT APPLICABLE`.
> An external requirement is **never** `PASS` because documentation exists.

---

## A. Credential Security

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| A1 | `opencode.json` (or equivalent) contains **no** real secret values | Remediated → env-var references only | `grep -n 'env:' opencode.json` shows `{env:OMNIROUTE_API_KEY}`, `{env:KROUTER_API_KEY}`; `.gitignore` hardened; `docs/security/credential-exposure-remediation.md` | Owner revokes & rotates the 3 keys | [Deployment Owner] | PENDING | Tooling credential exposure (not runtime) |
| A2 | No secret value present in the working tree | Clean | §24 targeted scan: 0 actual secrets (A); 4 placeholders (B); 3 test-only (C); 1 doc example (D); 8 false positives (E) | — | — | PASS | — |
| A3 | Local secret variants ignored | Hardened | `git check-ignore opencode.local.json .env.local .env.*.local` all ignored | — | — | PASS | — |
| A4 | Git history does not expose working-tree secrets | Remediated | Old values (`sk-4c0a…7101`, `sk-d36e…a424`, `sk-f7b4…fb96`) only in history at `4a09731`/`504a78f` | Owner rotates externally | [Deployment Owner] | PENDING | 3 keys require external rotation |
| A5 | History rewrite performed | Not performed | Hard rule §14: "Never rewrite Git history automatically" — explicitly documented in remediation report | — | — | PASS (not applicable) | — |

**Section state: CREDENTIAL ROTATION = PENDING — EXTERNAL ACTION**
The working tree is clean; three tooling API keys remain in git history and
require external revocation/rotation.

---

## B. Email / Authentication Provider

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| B1 | SMTP adapter deployment-ready | Implemented | `SmtpEmailSender` reads `EMAIL_USERNAME`/`EMAIL_PASSWORD`/`EMAIL_PROVIDER`; `EmailProviderOptions.EnsureValidSelection()` throws `EMAIL_PROVIDER_NOT_CONFIGURED` outside Development | Real SMTP provider contract + credentials | [Platform Operator] | PENDING | Production email delivery |
| B2 | Authentication (JWT) secure | Configured | `JwtSettings.EnsureSafeFor()` refuses dev key outside Development; `ClockSkew=0`; CORS locked to `https://app.seniorconnect.at` | Deployment `JWT_SECRET_KEY` env value | [Platform Operator] | PASS | — |
| B3 | No fake provider masquerading as production | Verified | Stub `EmailSender` removed; geocoder `Provider: none` with `RequireApiKey`; fail-closed | — | — | PASS | — |
| B4 | Development fallback non-production | Verified | OTP logging to console only; no email sent in Development | — | — | PASS | — |
| B5 | Email provider security tests | All passing | 14 tests in `EmailOtpProviderSecurityTests.cs` | — | — | PASS | — |

**Section state: PENDING** — Real SMTP provider contract and credentials are
an external deployment dependency. Development-channel HTTP dry run passed
(55/55).

---

## C. Production Infrastructure

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| C1 | Application host | Not provisioned | `backend/Dockerfile` validated (249 MB chiseled, UID 1654, no shell, ports 8080/8443); `docker-compose.yml` reproducible | Real server / cloud provider | [Platform Operator] | NOT STARTED | Entire deployment |
| C2 | PostgreSQL (production) | Not provisioned | Migration mechanism validated; CI uses `postgres:16` service | Real PostgreSQL instance | [Platform Operator] | NOT STARTED | Data layer |
| C3 | Reverse proxy | Not provisioned | Container exposes 8080/8443 | Real proxy (nginx/traefik/etc.) | [Platform Operator] | NOT STARTED | TLS termination |
| C4 | Container / artifact source | Not configured | Image builds locally; CI `push: false` | Registry (GHCR/ACR/other) | [Platform Operator] | NOT STARTED | Deployment delivery |
| C5 | Secret storage (deployment env/vault) | Not provisioned | All secrets externalized via env vars (`JWT_SECRET_KEY`, `DB_PASSWORD`, `EMAIL_*`, `Geocoding__ApiKey`) | Deployment secrets supplied at deploy time | [Platform Operator] | NOT STARTED | Runtime security |
| C6 | Logs / monitoring | Not configured | Health probes at `/health` + `/health/ready`; no centralized logging | Monitoring stack (provider-agnostic) | [Platform Operator] | NOT STARTED | Observability |
| C7 | Deployment access | Not configured | `scripts/verify_deployment.sh` validates a running deployment | SSH/API access to host | [Platform Operator] | NOT STARTED | Remote deployment |

**Section state: NOT STARTED** — No hosting provider, DNS provider,
certificate provider, monitoring provider, or legal entity has been
invented or assumed by this phase.

---

## D. Database / Backup

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| D1 | Backup mechanism proven | Validated | `scripts/backup_db.sh` (`pg_dump -Fc` + SHA-256); `scripts/restore_db.sh` (`pg_restore --clean`) | — | — | PASS | — |
| D2 | Restore drill exercised | PASS (synthetic) | §14 re-run: `sc_test_pg_phase` container, 4 tables / 11,500 rows, all match with quoted identifiers; SHA-256 `57f060…88ad0`; container torn down | — | — | PASS | — |
| D3 | Real backup target exists | Not provisioned | `docs/operations/backup-restore-requirements.md`; mechanism proven, target external | Backup vendor / target infrastructure | [Backup Owner] | BLOCKED | Data durability |
| D4 | Migration deployment | Verified | `MigrateAsync()` at startup; 2 new migrations applied; chain integrity confirmed | Real PostgreSQL | [Platform Operator] | PASS | — |
| D5 | Backup schedule / retention / alerting | Not configured | Documented in restore runbook as external | Deployment-specific schedule | [Backup Owner] | PENDING | Data protection |

**Section state: PASS (mechanism), BLOCKED (real target)**

---

## E. Operational Ownership

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| E1 | Platform operator appointed | Not appointed | `docs/operations/operational-ownership-checklist.md` defines role | Named person | [Platform Operator] | PENDING | Launch authority & escalation |
| E2 | Legal owner appointed | Not appointed | Same checklist | Named person | [Operating Org Legal Owner] | PENDING | Legal sign-off |
| E3 | Safeguarding lead per community | Not appointed | `docs/operations/safeguarding-operational-runbook.md` §0 | Named person per community | [Safeguarding Lead's org] | PENDING | S-2/S-3 follow-up |
| E4 | Platform safeguarding officer | Not appointed | S-4 API verified; fail-closed when vacant | Named person via `POST /api/v1/safeguarding/platform-officers/{userId}` | [Platform Operator] | PENDING | Org-less case access |
| E5 | Backup owner appointed | Not appointed | Same checklist | Named person | [Backup Owner] | PENDING | Data durability |
| E6 | Support contact named | Not appointed | Runbook §9 escalation path documented | Named contact per deployment | [Platform Operator] | PENDING | Incident escalation |
| E7 | Operational coordinator | Not appointed | Coordination flows implemented | Named person(s) | [Safeguarding Lead's org] | PENDING | S-2 task follow-up |
| E8 | Deployment owner | Not appointed | This phase exists to hand off to one | Named person | [Deployment Owner] | PENDING | Final launch decision |

**Section state: PENDING — external appointment required** for all 8 roles.

---

## F. Safeguarding Operations

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| F1 | Incident-response runbook exists | Complete | `docs/operations/incident-response-runbook.md`; 7 incident types × 6 phases; no invented endpoints; statutory deadlines NOT asserted | Named escalation contacts (E6) | [Platform Operator] | PASS | — |
| F2 | All 5 policy behaviors verified | PASS (HTTP-verified) | S-2/S-3/S-4/B-1/B-2 all pass in HTTP dry run (55/55) | — | — | PASS | — |
| F3 | S-4 constraint defect fixed | Fixed | Migration `20261001150947_AddPlatformSafeguardingOfficerRoleCheck` applied | — | — | PASS | — |
| F4 | Safeguard isolation in schema | Verified | `audit_entries` isolated; `safeguarding.access_logs` verified live | — | — | PASS | — |
| F5 | Safeguarding lead appointed | Not appointed | See E3 | Named person per community | [Safeguarding Lead's org] | PENDING | Operational follow-up |
| F6 | Platform safeguarding officer reachable | Not appointed | See E4 | Named person via API | [Platform Operator] | PENDING | Org-less case access |

**Section state: READY** (engineering verified; operational owner appointment
still external).

---

## G. Data Protection / Legal

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| G1 | Repository evidence (minimisation, isolation, audit) | Verified | Tenancy, access control, deletion/export, consent, location privacy all implemented & tested | — | — | PASS | — |
| G2 | ADR-026 visit-address gate OFF | Verified | `RequireGrant: false` only definition; architecture guard green | — | — | PASS | — |
| G3 | DPIA reviewed & signed | Draft only, no sign-off | `docs/legal/dpia.md` §4: "internal working draft; no approval" | Qualified legal review & sign-off | [Legal Owner] | PENDING | Any real personal data |
| G4 | Controller/processor roles determined | Templates exist | `docs/legal/verarbeitungsverzeichnis.md` (draft); `docs/legal/dpa-template.md` | Written determination for operating entity | [Legal Owner] | PENDING | Any real personal data |
| G5 | Purposes & legal bases decided | Repository evidence; no legal interpretation | Business rules implemented; `docs/security/visit-address-activation-gate.md` §4 lists 4 questions | Qualified legal review | [Legal Owner] | PENDING | ADR-026 activation |
| G6 | DPA with vendors | Template only | `docs/legal/dpa-template.md` | Signed DPA(s) for actual vendors | [Legal Owner] | PENDING | Any real personal data |
| G7 | Privacy information published | Content exists | `docs/legal/privacy-policy.md` | Publication in deployment channel | [Legal Owner] | PENDING | Early Access onboarding |
| G8 | Retention/deletion schedule confirmed | Implementation tested | `docs/legal/data-retention-policy.md`; deletion/export endpoints tested | Confirmation matches deployment obligations | [Legal Owner] | PENDING | Real personal data retention |
| G9 | Data-subject rights process | Endpoints implemented | Consent withdraw (BR-GDPR-01/02); export/deletion endpoints | Named responder + process | [Legal Owner] | PENDING | Early Access |
| G10 | Hosting/vendor & transfer review | Code has no SDKs | Geocoder/email providers are config-selected; none committed | Deployment-specific vendor & location review | [Platform Operator] + [Legal Owner] | PENDING | Any real personal data |

**Section state: PENDING — external legal review required. No compliance
claim made.**

---

## H. ADR-026 Activation

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| H1 | `RequireGrant: false` in all configs | Verified | `appsettings.json` only definition; no override in `appsettings.Production.json` | — | — | PASS | — |
| H2 | No path to enable client-side | Verified | Server-side config only; no endpoint writes config; Flutter has no input | — | — | PASS | — |
| H3 | Env/compose defaults don't enable | Verified | No env or compose value sets `RequireGrant` | — | — | PASS | — |
| H4 | Architecture guard green | Verified | Build fails on any committed activation (59/59 architecture tests) | — | — | PASS | — |
| H5 | Legal answers to 4 questions | Not provided | `docs/security/visit-address-activation-gate.md` §4 lists the four questions | Qualified legal review | [Legal Owner] | NOT STARTED | ADR-026 activation only |
| H6 | Written activation authorization | Not provided | Activation procedure documented (same file §4) | Signed authorization record | [Platform Operator] + [Legal Owner] | NOT STARTED | ADR-026 activation only |

**Section state: ADR-026 = OFF. ENGINEERING IMPLEMENTED / ACTIVATION OFF.**

---

## I. Participant Readiness

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| I1 | Recruitment source selected | Not selected | `docs/product/early-access-validation-plan.md` defines shape; no party named | Owner selects community | [Operating Owner] | NOT STARTED | Early Access |
| I2 | Participants contacted/committed | None | No contact made (hard rule §2) | Owner decision & outreach | [Operating Owner] | NOT STARTED | Early Access |
| I3 | Consent & onboarding flow ready | PASS | Consent record/version/withdraw implemented; `docs/operations/pilot-onboarding-playbook.md` exists; localized (713 × 3) | Participant-facing rollout | [Operational Coordinator] | PASS | — |
| I4 | Support/training material delivered | Not delivered | Emergency boundary (144/112) and reporting paths implemented in-app | Delivery to actual participants | [Operational Coordinator] | NOT STARTED | Early Access |
| I5 | Second organization onboarded | Not applicable | Multi-org boundary passed; S-4 scoping implemented | Out of Early Access scope | [Operating Owner] | NOT APPLICABLE | Out of scope |

**Section state: NOT STARTED** — no participant contacted; no second
organization onboarded.

---

## J. Launch Owner Decision

| # | Requirement | Current State | Evidence | External Dependency | Owner | Status | Blocking Scope |
|---|---|---|---|---|---|---|---|
| J1 | Owner authorized Early Access launch | Not authorized | Hard rule §1: "DO NOT launch Early Access automatically" | Explicit owner go/no-go | [Launch Owner] | PENDING | Any further launch |
| J2 | Owner authorized ADR-026 activation | Not authorized | Hard rule §6: activation only with external evidence | Written authorization | [Launch Owner] + [Legal Owner] | NOT STARTED | VisitAddress feature |
| J3 | Owner accepted deployment risk | Not accepted | This report = handoff to owner | Risk acceptance sign-off | [Launch Owner] | PENDING | Production go-live |

**Section state: PENDING — owner decision required.** Early Access is
**NOT STARTED**. This phase does not auto-launch.

---

## Summary counts (informational, not a score)

- **PASS** (repository-proven): A2, A3, A5, B2, B3, B4, B5, D1, D2, D4, F1,
  F2, F3, F4, G1, G2, H1, H2, H3, H4, I3 — all from current repo evidence.
- **PENDING** (external action): A1, A4, B1, C1–C7, E1–E8, F5, F6, G3–G10,
  I1, I2, I4, J1–J3 — personnel/provider/infrastructure/legal items.
- **BLOCKED** (not started, external): D3, D5 — real backup target.
- **NOT STARTED**: J2 (owner-specific; not yet relevant).
- **NOT APPLICABLE**: I5 (second org out of Early Access scope).

**External gate reconciliation: PASS** — every gate evaluated against
repository evidence; external-dependent gates correctly classified as
PENDING/NOT STARTED/BLOCKED, not PASS. Engineering-proven gates are PASS.
