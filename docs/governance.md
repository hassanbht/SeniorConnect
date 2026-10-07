# Governance (P9-10)

> SeniorConnect is developed and operated as an **independent open-source
> project** with no owning, funding, or administering organization
> ([ADR-028](decisions/ADR-028-project-independence.md)). This document defines
> how decisions are made without a vendor.

---

## 1. Roles

| Role | Who | Powers |
| --- | --- | --- |
| Maintainers | Contributors with merge rights to `main` | Merge/revert code, enforce CI and the Definition of Done, close issues |
| Contributors | Everyone | Propose changes via PR, comment on ADRs and issues |
| Operators | Whoever runs a deployment | Day-2 operations per [operator-runbook](operations/operator-runbook.md); decide deployment-local config; may risk-accept open product decisions **for their own deployment only** |
| Safeguarding officers | Appointed per operating organization | Access to the `safeguarding` schema for their org only ([safeguarding-scoping-policy](security/safeguarding-scoping-policy.md)) |

No role grants unilateral product-direction power; direction changes go through ADRs.

## 2. The ADR process

1. **Proposed** — open a PR adding `docs/decisions/ADR-NNN-<slug>.md` (status
   *Proposed*) with context, options, and consequences; add an index row in
   [docs/decisions/README.md](decisions/README.md).
2. **Discussion** — review on the PR; substantive objections must be answered in
   the document, not only in comments.
3. **Accepted / Rejected** — maintainers merge the status change; superseded
   ADRs get an amendment banner rather than silent edits (see ADR-020/021).
4. **Implementation** — only after *Accepted*; design-only plans may exist
   beforehand but must be clearly marked (see ADR-026 implementation plan).

Numbering is sequential; never renumber or delete an ADR.

## 3. Decision domains

| Domain | Decides | Examples |
| --- | --- | --- |
| Product & scope | Roadmap phases, parked items | [roadmap](product/roadmap.md), BUILD-CHECKLIST phases |
| Architecture | Data model, modularity, matching | [modular-monolith](architecture/modular-monolith.md), ADR-014 |
| Security & privacy | Gates, policies, legal docs | [security-overview](security/security-overview.md), Legal Gate |
| Branding & independence | Partner-neutrality rules | ADR-028, independence guard tests |

## 4. Open decisions register

Unresolved decisions are tracked openly, never worked around silently:
ADR-026 (visit-address sharing), ADR-027, and the S-2/S-3/S-4, B-1/B-2 items in
the [phase5 decision matrix](product/phase5-decision-matrix.md). Each names its
own promotion criteria; implementation plans stay *design only* until promoted.

## 5. Conflict resolution

1. Technical disagreement → try to converge in the PR; a maintainer may call a
   48-hour comment window.
2. Unresolved → an ADR with explicit options is the tiebreaker; the merge
   decision of the maintainers (documented in the ADR) stands.
3. Conduct: be respectful and assume good faith; safeguarding and privacy
   concerns always outrank feature velocity.

## 6. License & contributions

Contributions are accepted under the project license ([LICENSE](../LICENSE));
the contribution workflow is in [CONTRIBUTING.md](../CONTRIBUTING.md). The
project may relicense only with a maintainer supermajority recorded in an ADR.
