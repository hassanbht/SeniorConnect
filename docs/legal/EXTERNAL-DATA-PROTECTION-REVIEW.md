# External Data Protection / Legal Review

> **Status:** 2026-10-02 (launch-security & deployment-closure phase:
> repository evidence re-verified — cross-tenant reads answer 404, safeguarding
> isolation and access logs pass, ADR-026 gate re-confirmed `false`;
> external review items unchanged and still **PENDING**).
> This document **does not perform legal analysis** and **does not solve the
> Data Protection gate**. It separates (a) what this repository has actually
> implemented from (b) what requires qualified, external, deployment-specific
> review. **No compliance claim of any kind is made here** — neither
> "GDPR compliant" nor any equivalent, and no document in this repository
> constitutes lawyer approval.

## Repository Evidence

**Implemented** — what has actually been implemented, tested and is
verifiable in code. This section is **evidence only**; it is not a legal
opinion and satisfies no external requirement:

| Area | Evidence |
| --- | --- |
| **Data minimisation controls** | No ID documents or criminal-record extracts stored (verification records hold result/status/expiry only, BR-TRUST-05); no tracking/advertising SDKs; location stored as consent-gated disclosure levels (`NONE/APPROXIMATE/PRECISE`), exact coordinates only behind `GetHelpRequestLocationAsync`; contact/address masking BR-COMM-04. |
| **Tenant isolation** | Organization scoping server-side from the request/case rows (never client-supplied); cross-tenant reads answer 404; multi-org boundary tests green (59 architecture + module tests). |
| **Access control** | JWT with strict validation (`ClockSkew=0`), capability checks per endpoint, staff/admin/officer role separation, rate limiting on auth, fail-closed officer gates (S-4). |
| **Auditing** | Append-only `audit_entries` interceptor on state transitions; `safeguarding.access_logs` per safeguarding action (BR-SG-06); help-request status history; verified in the HTTP dry run (counts > 0). |
| **Location privacy** | Snap-to-grid for matching, disclosure policy per relationship, blocks/restrictions only ever subtract (BR-DISC-06); consent revocation effective immediately. |
| **Safeguarding isolation** | Separate schema + `SafeguardingDbContext`; case text never reaches `audit_entries`; org-less cases visible only to the appointed platform safeguarding officer (S-4, verified over HTTP this phase). |
| **Visit-address feature gate** | ADR-026 Option C implemented but gated `VisitAddress:RequireGrant=false` in every committed configuration, architecture-guarded; activation requires external written authorization (`docs/security/visit-address-activation-gate.md`). |
| **Deletion / export** | Two-tier deletion and data-export paths implemented and tested (PilotHardening suite). |
| **Consent** | Versioned consent with withdraw at registration (BR-GDPR-01/02), discoverability switch, location-sharing consent levels. |

These are engineering facts only. They are **not** a legal opinion and do
**not** satisfy any external requirement by themselves.

## External Review

**Pending** — every item is **PENDING / EXTERNAL ACTION REQUIRED** unless and
until authoritative deployment evidence exists (none exists as of this phase).
No legal conclusion is drawn here.

| # | Item | Status |
| --- | --- | --- |
| 1 | Actual controller/processor roles for the operating entity | PENDING / EXTERNAL ACTION REQUIRED |
| 2 | Purposes and legal bases (including any basis beyond consent) | PENDING / EXTERNAL ACTION REQUIRED |
| 3 | Privacy information provided to participants | PENDING / EXTERNAL ACTION REQUIRED |
| 4 | Retention/deletion periods confirmed against real obligations | PENDING / EXTERNAL ACTION REQUIRED |
| 5 | Data-subject rights process (who answers, how, within which deadline) | PENDING / EXTERNAL ACTION REQUIRED |
| 6 | DPA requirements with hosting and processing vendors | PENDING / EXTERNAL ACTION REQUIRED |
| 7 | International transfer considerations where applicable | PENDING / EXTERNAL ACTION REQUIRED |
| 8 | DPIA assessment/sign-off where applicable (`docs/legal/dpia.md` is a project-internal draft, not an approval) | PENDING / EXTERNAL ACTION REQUIRED |
| 9 | ADR-026 legal questions (four, listed in the activation-gate doc) | PENDING / EXTERNAL ACTION REQUIRED |
| 10 | Deployment-specific hosting/vendor review | PENDING / EXTERNAL ACTION REQUIRED |

**Overall Data Protection / Legal status: PENDING — external action
required.** No real personal data may be processed for the deployment until
items 1–6 are resolved in writing by the responsible external owner.
