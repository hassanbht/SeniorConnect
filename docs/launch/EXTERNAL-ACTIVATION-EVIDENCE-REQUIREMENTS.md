# SeniorConnect — External Activation Evidence Requirements

> **Recorded:** 2026-10-02 · **Phase:** Release Candidate Freeze & External
> Activation Evidence.
>
> This document defines **exactly** what evidence can move each external gate
> from `PENDING` / `BLOCKED` to `READY` / `PASS`. It is a contract for the
> external deployment and operating owner.
>
> ## The governing principle
>
> > **Documentation alone is NOT evidence of real-world deployment readiness.**
> >
> > A document that *describes* a capability proves the capability was
> > *designed*. Only an artifact produced by the *running system*, or a record
> > produced by the *responsible human authority*, proves it *exists*.
>
> No gate in this document may be moved by this agent, by a document, by a
> configuration file, or by a claim. It may be moved only by evidence listed
> here, verified by the named verifier.
>
> **No vendor is required or named.** Any provider meeting the stated
> capability is acceptable.
>
> **Hard stop:** this document authorizes nothing. It does not launch Early
> Access, does not activate ADR-026, and makes no legal determination.

---

## 0. Status vocabulary

| Status | Meaning |
| --- | --- |
| `PASS` | Proven by repository evidence (code + executed tests) |
| `READY` | Proven by accepted external evidence (this document's standard) |
| `PENDING` | Not started; the action is possible but has not begun |
| `BLOCKED` | Cannot proceed until a precondition external to the work is satisfied |
| `NOT STARTED` | Deliberately not begun (owner decision pending) |

`document exists` is **never** convertible to `gate passed`.

---

## 1. Evidence acceptance rules (apply to every gate)

1. **Provenance required.** Every artifact must state what produced it, when,
   and in which environment. An undated, unattributed file is not evidence.
2. **Environment must be real.** Local disposable containers, CI runners,
   developer machines and Development-channel runs are **not** production
   evidence. They may prove *mechanism* only, and are labelled as such.
3. **Synthetic data only.** Validation uses synthetic accounts, synthetic
   organizations and synthetic locations. Real personal data must never be used
   to prove readiness.
4. **No secret disclosure.** Evidence must never contain a credential, token,
   private key, OTP, exact private coordinate or safeguarding case content. Use
   masked identifiers and counts only.
5. **Verifier independence.** Whoever produced the evidence may not be the sole
   verifier of an approval. Rotation and restore are verified by a named owner
   other than the operator of the system where practical.
6. **Time-bound.** Evidence older than the review interval in §3 must be
   re-produced.
7. **Honest failure.** A gate that cannot produce evidence stays `PENDING` or
   `BLOCKED`. Simulated or reconstructed evidence is a phase failure.

---

## 2. Concrete acceptable-evidence examples (vendor-neutral)

These are the *kinds* of artifact that satisfy a gate. Any implementation that
produces the same assurance is equally acceptable.

| Capability | Acceptable evidence |
| --- | --- |
| Provider configuration | Provider configuration verified in a controlled environment, plus a successful real send to a mailbox controlled by the operator |
| Deployment existence | Deployment URL that resolves and answers the health probe, with the responding revision recorded |
| TLS | Certificate chain, validity dates and expected hostname captured from a live handshake |
| Backup | Backup run completed at a recorded time, with an artifact identifier and a checksum |
| Restore | Restore run completed into a clean target, with schema verification and application startup recorded |
| Role assignment | Role assignment confirmed in writing by the responsible owner, with contact path and deputy |
| Legal / Data Protection | Authoritative review recorded by the responsible authority (not by this repository, not by this agent) |
| Alert routing | A real alert delivered through the deployment's actual routing path to a real recipient |
| Incident readiness | A rehearsed path with recorded owner, backup contact, escalation step and evidence location |

---

## 3. Gate-by-gate evidence contract

Each gate states: **required evidence** · **acceptable source** · **verifier** ·
**status transition** · **review interval**.

---

### A. Credential Rotation

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL ACTION` |
| **Required evidence** | (1) Provider-side record that each exposed credential is revoked/rotated; (2) evidence that the **old** values no longer authenticate (e.g. a rejected-authentication result from the provider); (3) confirmation that replacements live only in the environment/secret store or an untracked local override; (4) a re-run tracked-secret scan showing zero plaintext credentials in the working tree |
| **Acceptable source** | The credential issuer's own dashboard/audit log; a re-run of the repository secret scan |
| **Verifier** | Deployment Owner, countersigned by Platform Owner |
| **Transition** | `PENDING` → `READY` only when (1)–(4) all exist |
| **Known scope** | Three credentials, in commits `4a09731` and `504a78f`; the same two also in `cc4d451`, reachable via `refs/cline/checkpoints/session_1790935206923_azxk9/1` (finding R-2). Remote exposure is **unverifiable** from this machine (`git ls-remote origin` → `Repository not found`) |
| **Expiry / review** | Re-verify at each release; immediately on any suspected exposure |
| **Prohibited** | Never print a credential value; never rotate automatically; never rewrite history as part of rotation |

---

### B. Authentication Provider

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL` (`Email:Provider = "none"` in both `appsettings.json` and `appsettings.Production.json`) |
| **Required evidence** | A synthetic end-to-end run in the deployment environment: registration → OTP/magic-link issuance → provider delivery → authentication → **expiration rejection** → **replay rejection** → **invalid-token rejection**. Plus confirmation that OTP is never logged, tokens are never logged, credentials are never logged, transient provider failure **fails safely**, and rate limiting remains active |
| **Acceptable source** | Deployment logs (masked), provider delivery receipt to an operator-controlled mailbox, application response codes |
| **Verifier** | Platform Operator, with Deployment Owner review |
| **Transition** | `PENDING` → `READY` when the full path passes in the real environment |
| **Expiry / review** | Re-run per environment change; on any provider change; at least per release |
| **Prohibited** | **No message may be sent to a real participant.** Synthetic mailbox only |
| **Repository note** | `SmtpEmailSender` and `EmailProviderOptions` are implemented and fail closed; `EnsureValidSelection()` raises `EMAIL_PROVIDER_NOT_CONFIGURED` outside Development. Mechanism evidence only |

---

### C. Production Infrastructure

| Field | Value |
| --- | --- |
| **Current status** | `BLOCKED — NOT PROVISIONED` |
| **Required evidence** | Existence and reachability of: host, environment designation, container runtime, deployment artifact, PostgreSQL, domain, TLS, reverse proxy, secret management, deployment access, and a responding health endpoint |
| **Acceptable source** | Live environment inspection; health-probe response with the responding revision |
| **Verifier** | Deployment Owner |
| **Transition** | `BLOCKED` → `READY` when all eleven elements are demonstrably present |
| **Expiry / review** | Re-verify at each infrastructure change |
| **Prohibited** | **Do not create a fake deployment environment to satisfy this gate.** A local container is not a deployment |

---

### D. PostgreSQL Deployment

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL` (no real instance provisioned) |
| **Required evidence** | Correct environment and database; migration version confirmed at `20261001150947_AddPlatformSafeguardingOfficerRoleCheck`; verified startup/migration behaviour; required extensions present; credentials externalized; TLS/network restriction as configured; no developer-database dependency. Plus minimal **synthetic** authorization smoke checks: tenant A → tenant A allowed where expected; tenant A → tenant B denied; ordinary admin → platform safeguarding case denied; platform officer → platform case allowed where policy says so |
| **Acceptable source** | Live query of `__EFMigrationsHistory`; startup log; response codes from synthetic requests |
| **Verifier** | Deployment Owner |
| **Transition** | `PENDING` → `READY` when migration head and the four authorization checks are evidenced |
| **Expiry / review** | Re-verify on any environment change |
| **Prohibited** | No real personal data. Do not re-run all historic PostgreSQL tests unless configuration or source changed |

---

### E. Backup

| Field | Value |
| --- | --- |
| **Current status** | `BLOCKED — NOT PROVISIONED` |
| **Required evidence** | A real target exists with: schedule, retention, encryption, access control, named owner, monitoring of failures, and a documented recovery procedure. If authorized, a synthetic backup execution recording **backup timestamp**, **artifact/backup identifier**, **result** and **verification method** |
| **Acceptable source** | Scheduler history; storage provider listing; checksum file; backup-owner record |
| **Verifier** | Backup Owner, with Platform Owner review |
| **Transition** | `BLOCKED` → `READY` when target, schedule, retention, encryption, access control, owner and monitoring all exist; `PENDING` → `READY` for the execution record |
| **Expiry / review** | Restore drill at least per release; backup-failure alerting verified per release |
| **Prohibited** | **Do not expose backup credentials.** Backups contain all personal data including the safeguarding schema |

---

### F. Observability

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL` |
| **Required evidence** | The **actual** environment can detect and route an alert for: application unavailable; database unavailable; authentication failure; email failure; deployment failure; migration failure; safeguarding workflow failure. Alert routing must be verified as real, not configured on paper |
| **Acceptable source** | A received alert with timestamp and recipient; monitor configuration export plus one triggered test alert |
| **Verifier** | Deployment Owner / Incident Escalation Contact |
| **Transition** | `PENDING` → `READY` when a real alert has been received through the real route |
| **Expiry / review** | Test alert per release; on any monitoring change |
| **Prohibited** | **Do not install a new observability platform** to close this gate; that is an owner procurement decision |
| **Repository note** | Health probes exist (`/healthz/live`, `/healthz/ready` with DB check, `/health`; container healthcheck). Detection of *failure* is not implemented in-repository |

---

### G. Operational Ownership

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL APPOINTMENT` (all eight roles) |
| **Required roles** | 1. Platform Owner · 2. Deployment Owner · 3. Safeguarding Lead · 4. Platform Safeguarding Officer · 5. Operational Coordinator · 6. Support Contact · 7. Incident Escalation Contact · 8. Backup Owner |
| **Required evidence** | For each role: responsible person or organization, contact path, named backup/deputy, and acknowledgement where applicable — recorded in writing by the Deployment Owner |
| **Acceptable source** | Written appointment record; for the Platform Safeguarding Officer, the audited API appointment `POST /api/v1/safeguarding/platform-officers/{userId}` |
| **Verifier** | Platform Owner |
| **Transition** | `PENDING` → `READY` only when **all eight** are real appointments |
| **Expiry / review** | On any change of holder; re-confirmed per release |
| **Prohibited** | **Do not invent names.** A placeholder remains `PENDING` |
| **Repository note** | `docs/operations/operational-ownership-checklist.md` defines all eight roles and the technical capability granting each. It appoints nobody |



---

### H. Safeguarding Ownership

| Field | Value |
| --- | --- |
| **Current status** | `PENDING` (engineering verified; operators not appointed) |
| **Required evidence** | The appointed operators can demonstrably perform S-2 (OperationalTask), S-3 (OperationalHold), S-4 (PlatformSafeguardingOfficer), B-1 (automatic assignment release) and B-2 (officer-only manual conversion) — evidenced by each designated operator demonstrating: where to access the control; what action they may perform; what information they must not expose; how escalation works; how ownership is handed over |
| **Acceptable source** | Recorded operator walkthrough with the actual appointed person, including a synthetic end-to-end exercise of each control |
| **Verifier** | Platform Safeguarding Officer + Safeguarding Lead |
| **Transition** | `PENDING` → `READY` when all five controls are demonstrated by appointed operators |
| **Expiry / review** | On any operator change; per release |
| **Prohibited** | **Do not create a training system.** A source-code test is not operational evidence |
| **Repository note** | Fail-closed by design: with no platform officer appointed, organization-less cases are inaccessible to everyone. Correct behaviour, not a defect |

---

### I. Data Protection / Legal

| Field | Value |
| --- | --- |
| **Current status** | `PENDING — EXTERNAL REVIEW` |
| **Required evidence** | Authoritative external review covering, tracked separately: controller/processor structure; purposes; legal bases; transparency/privacy information; retention/deletion; data-subject rights; processor agreements; transfer considerations; DPIA assessment; ADR-026 review; infrastructure/vendor review |
| **Acceptable source** | The responsible external authority's written record. **Not** this repository, not this agent, not a template |
| **Verifier** | The accountable legal owner for the operating entity |
| **Transition** | `PENDING` → `READY` only on that written record |
| **Expiry / review** | Per legal review cycle; on any change of controller, hosting location or vendor |
| **Prohibited** | **This agent must not perform legal analysis and must not claim GDPR compliance, a confirmed lawful basis, lawyer approval, DPIA approval, contractual compliance or insurance coverage** |
| **Repository note** | `docs/legal/EXTERNAL-DATA-PROTECTION-REVIEW.md` records repository implementation as *technical measures only*. `docs/legal/dpia.md` is an internal working draft, **not** an approval. Technical evidence is never converted into a legal conclusion |

---

### J. ADR-026 Activation

| Field | Value |
| --- | --- |
| **Current status** | `OFF` |
| **Required evidence** | **All five** of: (1) engineering implementation remains verified; (2) external activation evidence exists; (3) required Data Protection / Legal review is evidenced (gate I, including written answers to the four ADR-026 questions); (4) deployment owner is identified (gate G); (5) activation authority is explicit |
| **Acceptable source** | Written activation record naming deployment, date, approver, checklist version and the person flipping the flag (`docs/security/visit-address-activation-gate.md` §4) |
| **Verifier** | Platform Operator + Legal Owner |
| **Transition** | `OFF` → `READY-FOR-AUTHORIZED-ACTIVATION` when all five exist. **The feature is still not activated by that transition.** |
| **Expiry / review** | Per deployment; re-review on any legal or technical change |
| **Prohibited** | **Never activate automatically.** `VisitAddress:RequireGrant` remains `false`. Activation waits for explicit owner instruction |
| **Repository note** | `RequireGrant: false` is the only committed definition (verified in `appsettings.json`; no override in `appsettings.Production.json`; no env/compose value). The architecture guard `VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration` fails the build on any committed activation; green (59/59) in this phase |

---

### K. Participant Readiness

| Field | Value |
| --- | --- |
| **Current status** | `NOT STARTED` |
| **Required evidence** | A real community selected by the owner; participants actually prepared outside this agent; consent and onboarding flow delivered; support material delivered and acknowledged. Preparation must be performed by the operating owner — never by this agent |
| **Acceptable source** | The operating owner's own record of preparation |
| **Verifier** | Operating Owner |
| **Transition** | `NOT STARTED` → `READY` only on the owner's own record |
| **Expiry / review** | Per community; on any participant change |
| **Prohibited** | **Do not contact, recruit, or name any participant or organization.** Do not onboard a second real organization |
| **Repository note** | `docs/product/early-access-readiness-pack.md` states what must be in place. It does not start the program |

---

### L. Launch-Owner Authorization

| Field | Value |
| --- | --- |
| **Current status** | `PENDING` |
| **Required evidence** | An explicit, recorded owner decision covering: Early Access go/no-go; production go-live; deployment risk acceptance; and (separately) any ADR-026 activation |

---

## 4. Current state of every gate (as measured 2026-10-02)

| Gate | Status | Blocking condition |
| --- | --- | --- |
| A. Credential Rotation | `PENDING` | No rotation evidence exists |
| B. Authentication Provider | `PENDING` | `Email:Provider = "none"`; no provider contracted |
| C. Production Infrastructure | `BLOCKED` | No host, domain, TLS, registry or secret store |
| D. PostgreSQL Deployment | `PENDING` | No real instance provisioned |
| E. Backup | `BLOCKED` | No real target, schedule, retention or owner |
| F. Observability | `PENDING` | No monitoring stack or real alert route |
| G. Operational Ownership | `PENDING` | All eight roles unappointed |
| H. Safeguarding Ownership | `PENDING` | Operators not appointed / demonstrated |
| I. Data Protection / Legal | `PENDING` | No authoritative external review |
| J. ADR-026 | `OFF` | Legal answers + authorization absent |
| K. Participant Readiness | `NOT STARTED` | No community selected, nobody contacted |
| L. Launch-Owner Authorization | `PENDING` | No owner decision recorded |

> **No gate moved during this phase.** No external evidence was received. Each
> gate remains exactly as recorded, with the requirements above now made
> explicit and independently verifiable.

---

## 5. What this phase did **not** do

- Did not receive, simulate or reconstruct any external evidence.
- Did not provision a host, database, provider, backup target or monitoring.
- Did not contact any participant, organization, vendor or authority.
- Did not rotate or create any credential.
- Did not rewrite Git history and did not push.
- Did not activate `VisitAddress:RequireGrant`.
- Did not perform legal analysis or state any legal conclusion.
- Did not launch Early Access and did not decide the launch.

| **Acceptable source** | The owner's written instruction |
| **Verifier** | The owner is the authority; this agent is not |
| **Transition** | `PENDING` → `READY` only on the owner's explicit instruction |
| **Expiry / review** | Per decision |
| **Prohibited** | **The agent must not decide the launch.** Early Access must not start automatically |

