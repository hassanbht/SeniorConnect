# Architecture Decision Records

Format: one file per decision, `ADR-NNN-short-title.md`, never edited after acceptance —
superseded decisions get a new ADR that references the old one.

```md
# ADR-NNN — Title
Status: Proposed | Accepted | Superseded by ADR-XXX
Date: YYYY-MM-DD
## Context
## Decision
## Consequences
## Alternatives considered
```

## Accepted decisions

| # | Decision | Summary |
| --- | --- | --- |
| 001 | Modular monolith | ASP.NET Core modular monolith, not microservices. Boundaries enforced by architecture tests. Revisit only with data. |
| 002 | Shared-database multi-tenancy | Shared DB, shared schema, `organization_id` + EF global query filters. **Nullable** — community scope is a first-class case. |
| 003 | Trust ≠ Role ≠ Capability | Three separate concepts, three separate storage locations, computed server-side only. |
| 004 | Safeguarding is isolated | Separate PostgreSQL schema, separate DbContext, separate authorization policy. `OrganizationAdmin` does **not** imply access. |
| 005 | Geo: Haversine now, PostGIS later | lat/lng columns from day one, designed so `CREATE EXTENSION postgis` plus a generated column is purely additive. |
| 006 | Conditional UPDATE for assignment | `xmin` concurrency token generally; a conditional `UPDATE … WHERE status = 'offered'` for assignment. `RowsAffected = 0` → 409. |
| 007 | Org-optional core | Nothing in Community, Help or Family requires an organization. Organizations are an additive layer. |
| 008 | No verification documents stored | Only outcomes. The platform is a trust layer, not an identity provider. |
| 009 | Behaviour-based reliability, no public star ratings | Public 5-star ratings are the wrong instrument for a vulnerable-population platform. |
| 010 | Non-medical, unpaid scope, enforced in code | Blocked categories return a referral, never a request. Keeps the product out of Gewerbeordnung / care-regulation territory. |
| 011 | Dark mode + Senior Mode in Phase 1 | Both are theming/architecture concerns. Retrofitting them after 30 screens is expensive. |
| 012 | German is the localisation source of truth | de → en → it/fa. All keys authored in German first. |
| 013 | Identity provider behind an interface | `IIdentityVerificationProvider`. ID Austria is a future implementation, never a dependency of the pilot. |
| 014 | AI proposes, humans decide | No AI system may auto-assign a Safety-Level-3+ activity, in any phase. |
| 015 | Roadmap re-cut around the coordinator wedge | Hours, roster and impact reporting move from Phase 6 to Phase 2. Community and Family move later — they have zero interview evidence. Full ADR written. |
| 016 | Passwordless authentication | Phone + SMS OTP primary; password only for staff. Both known prior-tool failures were login failures. Full ADR written. |
| 017 | Funder is a distinct scope | A Gemeinde funds organizations, owns no users, and sees only aggregates with a minimum cohort of 10. Separate API namespace. Full ADR written. |

ADR-015, ADR-016 and ADR-017 exist as full files. Write 001–014 out as full ADR files
during Phase 0 — the table is the index, not the record.
