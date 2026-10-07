# SeniorConnect Release Candidate Activation Report

> **Date:** 2026-10-02 · **Phase:** Release Candidate Freeze & External
> Activation Evidence.
>
> This report separates **what this repository proves** from **what only an
> external deployment/operating owner can provide**. Engineering completeness
> never implies external approval.
>
> **Hard stop:** this phase did not launch Early Access, did not contact any
> participant or organization, did not activate `VisitAddress:RequireGrant`,
> did not rotate credentials, did not rewrite Git history, and makes **no**
> legal claim of any kind. The launch decision belongs to the human owner.

---

## 0. Canonical Cutover Verification update (2026-10-07 — supersedes the
finding R-1 state below)

The human canonical repository cutover has been performed and verified. The
previous finding R-1 (candidate not contained in HEAD of the old lineage) is
RESOLVED for the new canonical repository: HEAD `8bf2324` contains the
complete release candidate (source, tests, migrations, security controls,
approved-policy implementation, Flutter source, CI, Docker/build config).

> **FINAL UPDATE 2026-10-07 (release closure):** RDOCS-1 is now CLOSED
> (`b346a1d` + `9ccb5b6`) and the release revision has advanced. The table
> below reflects the current, verified state. Findings R-1 and R-4 in
> `PRODUCTION-ACTIVATION-GATE.md` are RESOLVED; no external gate changed
> status (no new external evidence was received).

| Dimension | Status (2026-10-07 verified) |
| --- | --- |
| **Engineering validation** | PASS — full suite re-executed by CI on the exact release SHA: 711/711 backend+architecture (0 fail, 0 skip, PostgreSQL executed), 909/909 Flutter, analyze clean, locale gate, dependency scan clean |
| **Canonical repository** | ESTABLISHED — `https://github.com/hassanbht/SeniorConnect.git` (private), branch `main`, remote HEAD == local HEAD |
| **Release revision** | `9ccb5b66021a29c02ddeced4d9ae74d969c476ab` (2026-10-07T20:20:23+02:00), frozen in `RELEASE-CANDIDATE-FREEZE.md`; lineage d0201b5 → 8dbdffb → 8bf2324 → b346a1d → 9ccb5b6 |
| **CI** | PASS — run 37666080520 head_sha == release SHA, 3/3 jobs success (prior doc commit b346a1d also green: run 37664705350) |
| **Artifact** | Local Release build SHA-256 `93D32267…A461` mapped to the release SHA (`RELEASE-BASELINE-EVIDENCE.md` §5); NO published/retained artifact yet (external/deployment concern) |
| **Production deployment** | **NOT DEPLOYED** |
| **External gates** | UNCHANGED — credential rotation, auth provider, infrastructure, domain/TLS, production DB, backup/restore, observability, operational & safeguarding ownership, Data Protection/Legal, participant readiness, owner launch authorization: all still externally controlled and unevidenced |
| **Early Access** | **NOT STARTED** |
| **ADR-026** | **OFF** (`VisitAddress:RequireGrant=false`, only committed definition; guard green on the exact release SHA) |
| **Documentation** | RDOCS-1 CLOSED — 114 `docs/` files tracked at the release SHA; provenance remains honest (historical docs NOT restored) |

---

## 1. Release Candidate

| Field | Value |
| --- | --- |
| **Version** | **No authoritative version exists.** Backend has no `<Version>`/`AssemblyVersion`, no `VERSION` file, no Git tag. Flutter is `1.0.0+1`. Candidate is labelled `RC-2026-10-02-WT-6f9c5ec` (descriptive label, **not** a release version). |
| **Source revision** | Base `6f9c5ec1567718ef8a5ff8626070aa1166739a68` (`main`) **+ 63 modified + 2 deleted + 98 untracked files — UNCOMMITTED** |
| **Artifact** | Local Release `SeniorConnect.Api.dll`, SHA-256 `AF9D30A3339AF4661121FCE9B0575180FE2BA2CE70B904383A06DD874A058B43`. No container image built or stored; no registry. |
| **Migration** | Head `20261001150947_AddPlatformSafeguardingOfficerRoleCheck`; 19-migration chain |
| **Build** | `dotnet build backend/SeniorConnect.slnx -c Release` → **0 errors / 0 warnings** (re-executed this phase) |

> **Finding R-1 (release integrity):** `HEAD` `6f9c5ec` does **not** contain
> the S-2, S-3, S-4, B-1, B-2, ADR-026 or SMTP-provider code the previous
> reports treat as released. It exists only in the uncommitted working tree.
> A deployment from `6f9c5ec` would ship none of the approved policy work.
> Detail: `docs/release/RELEASE-CANDIDATE-MANIFEST.md` §1.

---

## 2. Engineering Baseline

**PASS — verified against the actual candidate content this phase.**

| Check | Result | Note |
| --- | --- | --- |
| `dotnet build -c Release` | **0 errors / 0 warnings** | Re-run this phase |
| Backend regression vs **real PostgreSQL 16** | **612 passed / 0 failed / 0 skipped** | Re-run this phase (8 assemblies); 0 leftover `sc_test_%` databases; container removed |
| Architecture tests | **59/59** | Includes the ADR-026 feature-gate guard |
| Flutter analyze / test | **906/906** (prior evidence) | **NOT re-run** — no Flutter source changed |
| Locale check | **713 keys × 3 locales** (prior evidence) | **NOT re-run** — no translation file changed |
| HTTP synthetic dry run | **55/55** (prior evidence) | **NOT re-run** — needs a live API process; no source changed |
| Docker image | Dockerfile validated in CI (`push: false`) | **NOT rebuilt** — Dockerfile unchanged |
| Synthetic restore drill | PASS (prior phase) | **Not real operational evidence** — see §6 |

**Previous evidence:** `docs/launch/FINAL-ENGINEERING-HANDOFF.md`,
`docs/launch/PRODUCTION-ACTIVATION-READINESS-REPORT.md` §2.

**New evidence since freeze:** the Release build and the full 612-test backend
suite were **re-executed against the working tree**, because the candidate
differs from `HEAD` (finding R-1) and the prior runs were reported against a
commit that does not contain this code. Result: **0 failures**. No other suite
was re-run, because no Flutter, translation, Docker or configuration input

---

## 3. Security

| Field | Value |
| --- | --- |
| **Status** | **PASS (repository) / PENDING (rotation)** |
| **Credential state** | **PENDING — EXTERNAL ACTION.** Three credentials (`sk-4c0a…7101`, `sk-d36e…a424`, `sk-f7b4…fb96`) remain in Git history. No rotation evidence exists. **Not rotated by this phase.** |
| **Secret hygiene** | **PASS.** `opencode.json` holds only `{env:OMNIROUTE_API_KEY}` / `{env:KROUTER_API_KEY}`. `git grep -E "sk-[A-Za-z0-9]{16,}"` over tracked files → **0 matches**. Working-tree scan of `.json/.cs/.md/.yml/.sh/.ps1/.py/.dart` → **0 raw keys**. `.gitignore` covers `opencode.local.json`, `.env.local`, `.env.*.local`, `*.local.env`; `git check-ignore` confirms all three. |
| **Finding R-2** | A **fourth** historical location exists that the remediation record does not mention: commit `cc4d451` (`index on main: 6f9c5ec`), reachable via `refs/cline/checkpoints/session_1790935206923_azxk9/1`, containing the **same two** keys (masked comparison only). Not an ancestor of `main`; remote state unverifiable (`git ls-remote origin` → `Repository not found`). The remediation record's "54 commits" describes `main` only. |
| **Security validation** | 612/612 backend tests green including authorization, tenant-isolation, safeguarding-isolation and audit suites; 59/59 architecture guards green. No OTP, token, credential or private coordinate logged. No development environment masquerades as production: `Email:Provider = "none"`, `Geocoding:Provider = "None"` with `RequireApiKey: true`. |
| **No secret printed** | No credential value was printed at any point; all comparisons used masked identifiers. |

---

## 4. Authentication

| Field | Value |
| --- | --- |
| **Provider** | **`none`** — `Email:Provider = "none"` in both `appsettings.json` and `appsettings.Production.json` (verified this phase) |
| **Validation** | **Not performed against a real provider** — no provider exists to validate against |
| **Status** | **PENDING — EXTERNAL** |
| **Repository evidence** | `SmtpEmailSender` + `EmailProviderOptions` implemented; fails closed — `EnsureValidSelection()` raises `EMAIL_PROVIDER_NOT_CONFIGURED` outside Development and `OptionsValidationException` at startup on an incomplete SMTP selection. The fake-success stub `EmailSender.cs` is **deleted** in the working tree (still present in `HEAD`). 14 security tests green. OTP/token never logged; rate limiting active. |
| **Required before `READY`** | Full synthetic path in a real environment: issue → deliver → authenticate → expiration / replay / invalid rejection. **No message to any real participant.** Contract: `docs/launch/EXTERNAL-ACTIVATION-EVIDENCE-REQUIREMENTS.md` §B |

---

## 5. Production Infrastructure

| Field | Value |
| --- | --- |
| **Host** | **NOT PROVISIONED** |
| **Domain** | **NOT PROVISIONED.** `https://app.seniorconnect.at` exists only as a CORS **configuration string** in `appsettings.Production.json` and as API base-URL defaults in `main_prod.dart` / `main_staging.dart`. **No DNS, no reachability, no deployment is claimed.** |
| **TLS** | **NOT PROVISIONED.** No certificate, no terminator; no handshake was tested. |
| **Artifact** | **NOT PUBLISHED.** Dockerfile validated; CI builds with `push: false`; no registry, no image digest. |
| **Database** | **NOT PROVISIONED.** |
| **Secrets** | **Repository side PASS** — all secret inputs are environment variables (`JWT_SECRET_KEY`, `DB_PASSWORD`, `EMAIL_*`, `Geocoding__ApiKey`); no secret in any committed config; `docker-compose.yml` requires `JWT_SECRET_KEY` via `:?`. **No secret store instance exists.** |
| **Status** | **BLOCKED — NOT PROVISIONED** |

No hosting, DNS, certificate, registry or monitoring provider was invented,
selected or assumed.

---

## 6. Backup / Restore

| Field | Value |
| --- | --- |
| **Target** | **NONE — NOT PROVISIONED.** No storage, vendor, schedule or retention exists. |
| **Backup status** | **BLOCKED.** Scripts exist (`scripts/backup_db.sh` — `pg_dump -Fc` + SHA-256 sidecar). **No backup was executed this phase**, because no authorized target exists. |
| **Restore status** | **PENDING — EXTERNAL.** No restore was performed. Only an **authorized non-production or explicitly authorized recovery environment** may be used. |
| **Evidence** | Mechanism evidence only: a **synthetic** drill in a disposable container from a previous phase (4 tables / 11,500 rows, SHA-256 `57f060…88ad0`). **This proves the mechanism, not operational recovery readiness, and is not counted as real restore evidence.** |
| **Owner** | **PENDING — EXTERNAL APPOINTMENT.** No [Backup Owner] exists, so no drill can be authorized. |

Required before `READY`: a real target with schedule, retention, encryption,
access control, owner and failure monitoring, plus one recorded restore
verifying `organizations`, memberships, HelpRequests, assignments, audit
entries, safeguarding cases, `safeguarding.access_logs`, operational tasks and
`VisitAddressGrant` records — **counts only, never row contents**.

---

## 7. Observability

| Field | Value |
| --- | --- |
| **Application** | **PASS (detection surface only).** `/healthz/live`, `/healthz/ready` (incl. DB check), `/health`; container `healthcheck` via `dotnet SeniorConnect.Api.dll --healthcheck`. |
| **Database** | **PENDING — EXTERNAL.** DB health is part of `/healthz/ready`; no centralized alerting. |
| **Authentication** | **PENDING — EXTERNAL.** Rate-limiter `OnRejected` writes structured logs; no alert route. |
| **Notification** | **PENDING — EXTERNAL.** `EMAIL_PROVIDER_SEND_FAILED` / `EMAIL_PROVIDER_NOT_CONFIGURED` are truthful failure signals; no alerting pipeline. |
| **Safeguarding** | **PENDING — EXTERNAL.** Append-only `audit_entries` + `safeguarding.access_logs` exist; no anomaly alerting. |
| **Status** | **PENDING — EXTERNAL.** No monitoring stack, no log aggregation, and **no real alert has ever been received**. No new observability platform was installed. |

---

## 8. Operational Ownership

| # | Role | Status |
| --- | --- | --- |
| 1 | Platform Owner | **PENDING — EXTERNAL APPOINTMENT** |
| 2 | Deployment Owner | **PENDING — EXTERNAL APPOINTMENT** |
| 3 | Safeguarding Lead | **PENDING — EXTERNAL APPOINTMENT** |

---

## 9. Safeguarding Operations

| Policy | Engineering | Operational |
| --- | --- | --- |
| **S-2** OperationalTask | **PASS** — content-free, leak-free; green in this run | **PENDING** — no Safeguarding Lead or Coordinator appointed |
| **S-3** OperationalHold | **PASS** — neutral roster boolean; green in this run | **PENDING** — no Coordinator appointed |
| **S-4** PlatformSafeguardingOfficer | **PASS** — audited appointment API; org officer gets 404 (fail-closed) | **PENDING** — no platform officer appointed |
| **B-1** automatic assignment release | **PASS** — block releases the assignment; green in this run | Not role-dependent |
| **B-2** officer-only manual conversion | **PASS** — officer-only, audited; green in this run | **PENDING** — no officer appointed |

**Operational status: PENDING.** Engineering is verified; **no operator has
been demonstrated performing any control.** No operator walkthrough or training
system was created. With no platform officer appointed, organization-less cases
are inaccessible to everyone — **fail-closed by design, not a defect**.

Incident-response acceptance: the runbook defines owner, backup, contact,
escalation, evidence and recovery for all seven required paths (credential
compromise, authentication failure, unauthorized access, safeguarding incident,
location/privacy incident, database outage, failed deployment). Every path is
**PENDING on real owners** — the roles are placeholders. **No statutory
deadline is asserted anywhere.**

---

## 10. Data Protection / Legal

**No legal conclusion is drawn. No legal analysis was performed.**

### Repository evidence (technical measures only)

Data minimisation (no ID documents or criminal-record extracts); server-side
tenant scoping with cross-tenant reads answering 404; JWT with `ClockSkew=0`
and per-endpoint capability checks; append-only `audit_entries` plus
`safeguarding.access_logs`; snap-to-grid location privacy with
`NONE/APPROXIMATE/PRECISE` consent levels; separate `safeguarding` schema with
case text never reaching `audit_entries`; two-tier deletion and export;
versioned consent with withdrawal; ADR-026 implemented but **gated off**.

### External evidence

**NONE.** All ten items in
`docs/legal/EXTERNAL-DATA-PROTECTION-REVIEW.md` remain
**PENDING / EXTERNAL ACTION REQUIRED**: controller/processor roles; purposes
and legal bases; privacy information; retention/deletion; data-subject rights;
processor agreements; transfers; DPIA assessment; the four ADR-026 questions;
hosting/vendor review.

---

## 11. ADR-026

| Field | Value |
| --- | --- |
| **Engineering state** | **PASS.** `VisitAddressGrant` entity + `visit_address_grants` table; grant/revoke endpoints; address released to the live assignee only while a non-revoked grant exists **and** the gate is on. |
| **Current flag** | **`VisitAddress:RequireGrant = false` — OFF.** Verified this phase: the only committed definition is in `backend/SeniorConnect.Api/appsettings.json`; **no** override in `appsettings.Production.json`; **no** `VisitAddress__RequireGrant` in `docker-compose.yml`; no endpoint writes configuration; Flutter has no input. The architecture guard `VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration` is **green (59/59)** — it fails the build on any committed activation. |
| **External authorization state** | **NOT STARTED.** No written legal answers to the four ADR-026 questions; no signed activation record; no identified deployment owner. |
| **This phase** | **Did not activate the feature and did not change the flag.** Activation remains conditional on explicit owner instruction after gates G and I are satisfied. |

The seven approved decisions are unchanged: ADR-027 = A, S-2 = B, S-3 = B,
S-4 = B, B-1 = B, B-2 = A, ADR-026 = C.

---

## 12. Controlled Early Access

| Field | Value |
| --- | --- |
| **Scope** | **FROZEN.** `docs/product/controlled-early-access-scope.md` lists only implemented functionality: authentication, profile, organizations/memberships, discoverability, help requests, matching, proposals, assignments, messaging/reporting, community/groups, safeguarding controls, operational coordination, hours/reporting, emergency boundary. |
| **Explicitly excluded** | AI matching · payments · marketplace · emergency service / medical functionality · voice/IVR · corporate volunteering · white-label · visit-address sharing (gate OFF) · any future roadmap feature. **No feature was added in this phase.** |
| **Safety boundaries** | In-app emergency boundary (144/112; explicitly **not** an emergency service); block/report with 404-never-confirm; BR-COMM-04 withholding; assignment termination ends protected-data access; safeguarding reporting; consent levels; visit-address gate OFF. |
| **Stop conditions** | Defined in `docs/product/controlled-early-access-stop-conditions.md` — technical T1–T8, operational O1–O6, owner decision R1. **No invented KPIs.** |
| **Participant state** | **NOT STARTED.** No participant, senior, family, volunteer, community, partner or second organization was named, contacted, recruited or onboarded. **No real participant data was used.** |

---

## 13. Production Smoke Test

| Field | Value |
| --- | --- |
| **Environment** | **NONE EXISTS.** No staging or production environment is provisioned. |
| **Result** | **NOT EXECUTED — PENDING — EXTERNAL.** |
| **Evidence** | None. |

The §22 flow (deploy → verify revision → environment → DB → migration → health
→ authentication → discoverability → help request → matching → proposal →
assignment → block → B-1 → report → B-2 → S-2 → S-3 → S-4 → audit → cleanup)
**cannot be executed** without a real environment. The Development-channel HTTP
dry run (55/55, prior phase) is **engineer-validated synthetic evidence, not a
production smoke test**, and is not counted as one.

> **No fake deployment environment was created to satisfy this gate.**

---

## 14. Rollback / Recovery

| Field | Value |
| --- | --- |
| **Result** | **PENDING — EXTERNAL.** |
| **Evidence** | `docs/operations/release-and-rollback-runbook.md` documents application, configuration, feature-gate, database, shutdown and restart procedures using only commands that exist in this repository. **No procedure was executed against a real deployment, because none exists.** |
| **Missing capabilities** | Versioned artifact registry and retention (CI does not push) · deployment automation to a server · blue-green/canary promotion · centralized monitoring and alerting · backup scheduling and failure alerting · **and, critically, rollback-to-revision is impossible while the candidate is uncommitted (finding R-1)** |

> A document-only procedure is **not** called operationally validated.

---

## 15. External Activation Gates

| Gate | Scope | Status | Evidence source | Verification date | Owner | Next action |
| --- | --- | --- | --- | --- | --- | --- |
| **A. Credential Rotation** | 3 tooling keys (+ 1 further history location, R-2) | **PENDING** | None; no rotation evidence exists | 2026-10-02 | [Deployment Owner] | Revoke/rotate all keys; prove old values rejected |
| **B. Authentication Provider** | Real SMTP/email provider | **PENDING** | `Email:Provider = "none"` in both configs | 2026-10-02 | [Platform Operator] | Contract provider; provision credentials; run synthetic path |
| **C. Production Infrastructure** | Host, runtime, artifact, DB, domain, TLS, proxy, secrets, access, health | **BLOCKED** | None exists | 2026-10-02 | [Platform Operator] | Provision; then verify each of the eleven elements |
| **D. PostgreSQL Deployment** | Real instance, migration head, authz smoke | **PENDING** | None exists | 2026-10-02 | [Platform Operator] | Provision; verify `20261001150947`; run 4 authorization checks |
| **E. Backup** | Real target, schedule, retention, encryption, owner, monitoring | **BLOCKED** | None exists | 2026-10-02 | [Backup Owner] | Provision target; appoint owner; record one restore |
| **F. Observability** | 7 detection signals with real alert routing | **PENDING** | Health probes only; no alert ever received | 2026-10-02 | [Deployment Owner] | Provision monitoring; receive a real test alert |
| **G. Operational Ownership** | 8 roles | **PENDING** | Checklist rows all `_(not known)_` | 2026-10-02 | [Platform Owner] | Appoint 8 primaries + deputies in writing |
| **H. Safeguarding Ownership** | S-2/S-3/S-4/B-1/B-2 performed by real operators | **PENDING** | Runbook placeholders only | 2026-10-02 | [PSO] + [Safeguarding Lead] | Demonstrate each control with appointed operators |
| **I. Data Protection / Legal** | 10 external review items | **PENDING** | Repository technical measures only | 2026-10-02 | [Legal Owner] | Obtain authoritative external review |
| **J. ADR-026** | Visit-address gate | **OFF** | `RequireGrant: false`; guard 59/59 green | 2026-10-02 | [Platform Operator] + [Legal Owner] | Nothing — do **not** activate |
| **K. Participant Readiness** | Community, onboarding, support material | **NOT STARTED** | Nobody contacted | 2026-10-02 | [Operating Owner] | Select community and prepare participants (owner only) |
| **L. Launch-Owner Authorization** | Go/no-go, risk acceptance | **PENDING** | No decision recorded | 2026-10-02 | [Launch Owner] | Record explicit decision |

**Release-integrity items (repository, owner decision):** R-1 candidate
uncommitted · R-2 additional history location · R-3 no backend version / SDK
pin / dependency lock.

---

## 16. Remaining Blockers

Only genuine blockers are listed. None is an invented or simulated item.

**Repository / release integrity (fixable by the owner, no external vendor):**

1. **R-1** — the release candidate is uncommitted; `HEAD` does not contain the
   approved-policy implementation. No reproducible source revision exists, and
   rollback-to-revision is impossible.
2. **R-3** — no backend version, no SDK pin, no dependency lock, no config
   schema version, no embedded build timestamp.
3. **R-4** — `docs/release/` is git-ignored by the unanchored `[Rr]elease/`
   pattern, so the mandatory release records cannot be committed from their
   current location. Combined with R-1, a fresh clone yields neither the
   approved code nor its release record.

**Security (external owner action):**

4. Credential rotation of the three exposed keys (plus the R-2 location) is
   not done and cannot be done by this agent.

**External — infrastructure and operations:**

5. No application host, PostgreSQL instance, domain, TLS certificate, reverse
   proxy, artifact registry or secret store exists.
6. No real authentication/email provider is contracted.
7. No real backup target, schedule, retention, encryption or alerting exists,
   and no real restore drill has been performed.
8. No monitoring, log aggregation or alert routing exists.
9. None of the eight operational roles is appointed — which also blocks the
   gates that require a Deployment Owner or Backup Owner.

**External — legal and people:**

10. No authoritative Data Protection / Legal review exists (10 items).
11. No participant, community or second organization exists or was contacted.
12. No launch-owner decision has been recorded.

---

## 17. Owner Decision

**PENDING**

No launch authorization, no ADR-026 activation authorization and no deployment
risk-acceptance sign-off has been received. The Early Access launch decision,
the production go-live decision and the ADR-026 activation decision belong
**exclusively to the human operating owner**.

**This agent does not decide the launch, and did not attempt to.**

---

## 18. Final Result

# BLOCKED BY EXTERNAL GATES

The repository is engineering-ready and secret-clean, and the release candidate
is now explicitly identified, frozen and reproducible from its working tree.
**Every remaining blocker is external** — with one repository-side exception
that the owner must resolve first: **the candidate is not committed** (R-1), so
no deployment could be traced to, or rolled back to, a revision.

**Early Access was not launched. No participant or organization was contacted.
No credential was rotated. No Git history was rewritten. No legal compliance is
claimed. `VisitAddress:RequireGrant` remains `false`.**



### Open items

1. `docs/legal/dpia.md` is an **internal working draft, not an approval**.
2. `docs/legal/dpa-template.md` is a **template, not a signed agreement**.
3. No controller/processor determination exists for any operating entity.
4. No hosting or vendor has been selected, so no transfer review is possible.

> Technical measures are **never** converted into a legal conclusion. This
> report claims **no** GDPR compliance, **no** confirmed lawful basis, **no**
> lawyer approval, **no** DPIA approval, **no** contractual compliance and
> **no** insurance coverage.

| 4 | Platform Safeguarding Officer | **PENDING — EXTERNAL APPOINTMENT** |
| 5 | Operational Coordinator | **PENDING — EXTERNAL APPOINTMENT** |
| 6 | Support Contact | **PENDING — EXTERNAL APPOINTMENT** |
| 7 | Incident Escalation Contact | **PENDING — EXTERNAL APPOINTMENT** |
| 8 | Backup Owner | **PENDING — EXTERNAL APPOINTMENT** |

**Missing assignments: all eight.** Verified this phase that
`docs/operations/operational-ownership-checklist.md` still shows `_(not known)_`
for person, contact and backup in every row. **No name was invented.** The
checklist defines responsibilities and the technical capability granting each
role; it appoints nobody.

> **Consequence:** the Deployment Owner required by gates C, D and J and the
> Backup Owner required by gate E **do not exist**, so those gates cannot be
> advanced by anyone working in this repository.


changed (regression policy §23).
