# Security & Privacy Overview (P9-10)

> **Purpose:** the entry point to SeniorConnect's security and privacy
> documentation — what is protected, how, and where each rule is specified.
> Operator-specific hardening steps live in the [deployment guide §4](../operations/deployment-guide.md#4-eu-hosting-checklist-real-personal-data).
>
> Related: [privacy-gdpr](../architecture/privacy-gdpr.md),
> [trust-safety](../architecture/trust-safety.md),
> [ADR-028](../decisions/ADR-028-project-independence.md).

---

## 1. Threat model in one view

SeniorConnect processes real personal data about **vulnerable people** (seniors),
their families, and volunteers, including precise locations and home addresses.
Core risks: unauthorized access to senior data, location stalking, unsafe
volunteer contact with seniors, and cross-tenant data leakage in multi-org
deployments. The platform is explicitly **not** a medical or emergency service.

## 2. Defense layers and their documents

| Layer | Guarantee | Document |
| --- | --- | --- |
| Authentication | Multi-provider identity, phone OTP, JWT with server-side secret (≥ 32 chars, required outside Development) | [ADR-016](../decisions/ADR-016-passwordless-auth.md), deployment guide §3 |
| Password storage | PBKDF2-HMAC-SHA256 at 600,000 iterations (OWASP recommended value; recalibrate with a benchmark, do not treat as an absolute floor), stored as a self-describing `pbkdf2-sha256$<iterations>$<salt>$<hash>` record; legacy records re-derived on next login | `PasswordHasher`, BR-SEC-W1-08 |
| Identifier hashing | OTP / phone / email / IP stored only as HMAC-SHA256 under a server-held `IDENTITY_HASH_PEPPER` (≥ 32 chars, required outside Development); deterministic so DB lookups still work | [privacy-gdpr §6.1](../architecture/privacy-gdpr.md), BR-SEC-W1-05 |
| External identity verification | Off by default and fail-closed outside Development; a provider that cannot substantiate a result never reports success | BR-SEC-W1-06 |
| Authorization | Capability-based; 401 / 403 / 404-tenant / 200 tested per task ([authorization](../architecture/authorization.md)) | authorization.md |
| Tenant isolation | Organization-scoped queries; foreign-tenant lookups return 404, never 403 | [data-model §7](../architecture/data-model.md#7-organizations), operator runbook §4 |
| Location privacy | Explicit, revocable precise-location consent; visibility rules per role | [location-consent-and-visibility](location-consent-and-visibility.md), BR-GEO-* |
| Safeguarding | Separate `safeguarding` schema, separate access policy, officer-only | [safeguarding-scoping-policy](safeguarding-scoping-policy.md), [safeguarding-location-policy](safeguarding-location-policy.md) |
| Abuse handling | Report/block flows with defined response paths | [block-report-policy](block-report-policy.md) |
| Verification | Trust tiers 1–3 with document checks | [verification-and-discovery](verification-and-discovery.md) |
| Input & API hardening | ProblemDetails with stable codes, rate limiting, validation | [api-conventions](../api/api-conventions.md), [error-handling](../api/error-handling.md) |
| Mobile | OWASP MASVS / ASVS L2 assessment | [owasp-masvs-asvs-l2-assessment](owasp-masvs-asvs-l2-assessment.md) |
| Dependency & container security | CI vulnerability scan (P7-11), chiseled non-root runtime image | [ci.yml](../../.github/workflows/ci.yml), deployment guide |
| Availability & recovery | Nightly encrypted backups, SHA-256 checks, DR rehearsal | [backup-and-restore](../operations/backup-and-restore.md) |

## 3. Privacy (GDPR) map

| Topic | Document |
| --- | --- |
| What we process and why | [verarbeitungsverzeichnis](../legal/verarbeitungsverzeichnis.md), [privacy-policy](../legal/privacy-policy.md) |
| DPIA (high-risk processing) | [dpia](../legal/dpia.md) |
| Retention & deletion | [data-retention-policy](../legal/data-retention-policy.md) |
| Processor relationships (org ↔ operator) | [dpa-template](../legal/dpa-template.md) |
| Consent model (location, visibility) | [location-consent-and-visibility](location-consent-and-visibility.md) |

## 4. Security gates in the lifecycle

- **Legal Gate** — mandatory before any real personal data enters a deployment ([BUILD-CHECKLIST](../plans/BUILD-CHECKLIST.md)).
- **Phase 5 Security Gate** — feature-level gate for privacy-sensitive features ([phase5-security-gate](phase5-security-gate.md)).
- **Per-task DoD** — authorization matrix (401/403/404/200), concurrency, audit entries on every state transition ([BUILD-CHECKLIST](../plans/BUILD-CHECKLIST.md#per-task-definition-of-done)).
- **Independence guard** — architecture tests keep partner branding out of code ([PilotIndependenceGuardTests.cs](../../tests/architecture/SeniorConnect.ArchitectureTests/PilotIndependenceGuardTests.cs)).

## 5. Reporting vulnerabilities

This is an independent open-source project without a vendor (ADR-028). Report
suspected vulnerabilities privately to the maintainers' security contact listed
in [SECURITY.md](../../SECURITY.md) — do not open public issues for
exploitable findings. Safeguarding emergencies go to the responsible
safeguarding officer first.
