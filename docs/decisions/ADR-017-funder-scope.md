# ADR-017 — Funder is a distinct scope, not an organization type

Status: Accepted
Date: 2026-08
Refines ADR-002 (multi-tenancy) and ADR-007 (org-optional core).

## Context

The original data model treated a municipality as
`organizations.type = 'Municipality'` — an ordinary tenant that happens to be
public-sector.

Discovery F7 shows this is wrong in every respect:

> "We don't recruit volunteers directly, we fund the associations. We don't have
> transparent data and we don't know how many people are actually active."
>
> "Data must be fully anonymised. The municipality must never see the seniors'
> names in an analytics dashboard."

A Gemeinde owns no users, employs no volunteers, runs no activities, and is
legally required not to see the individuals. It funds organizations and reports
upward to a Gemeinderat. Modelling it as a tenant would give it a data surface it
must not have, and a workflow surface it has no use for.

It is also, per F9, the **fastest first sale** — a council committee decision is
smaller than an NGO procurement cycle. So this is commercially load-bearing, not
a modelling nicety.

## Decision

Introduce **Funder** as a distinct scope with its own relationship and its own
read-only, aggregate-only surface.

```
funders
  id · name · type (Municipality|Foundation|PublicBody|Corporate) · contact

funding_relationships
  id · funder_id · organization_id · valid_from · valid_until
  reporting_scope jsonb        -- which metrics, which geography, which programme
```

Hard rules, enforced in code and tested:

1. A funder principal can reach **only** aggregate endpoints. There is no code
   path from a funder token to a row containing a name, address, phone number,
   email, or any free-text field written by a user.
2. Every aggregate is suppressed below a **minimum cohort size of 10**. A cell
   with 1–9 renders as `<10`, never as the number, and never as zero-with-a-gap
   that permits inference by subtraction.
3. A funder sees only the organizations it funds, only within the
   `reporting_scope` and date range of an active `funding_relationship`.
4. Funder access is audited like any other access.
5. The funder surface is a **separate API namespace** (`/api/v1/funder/...`)
   backed by dedicated aggregate queries — not the organization endpoints with a
   filter bolted on. Sharing the endpoints would make rule 1 a matter of
   discipline rather than architecture.

An architecture test asserts that no DTO reachable from the funder namespace
contains a field classified as `PersonalData` or above.

## Consequences

- One extra small module and one extra API namespace.
- The Gemeinde becomes independently sellable: a funder can subscribe to the
  dashboard even before any of its funded organizations use the workflow tools —
  though the dashboard will be empty until they do, which is worth being honest
  about in the sales conversation.
- `organizations.type = 'Municipality'` is removed. A municipality that *does*
  directly run a volunteer programme registers as both a funder and an
  organization; these are separate records with separate access.
- Pricing follows the scope: funders pay a flat annual fee (F9 red line —
  per-seat SaaS pricing is explicitly rejected by small municipalities).
