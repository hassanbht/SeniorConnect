# ADR-002 — Shared-Database Multi-Tenancy

* **Status:** Accepted
* **Date:** 2026-08-22

## Context

SeniorConnect (now SeniorConnect) needs to support multiple independent organizations (tenants), but must also operate as an open platform in areas with no participating organization (independent or community mode).

## Decision

We will implement a **shared-database, shared-schema** multi-tenancy model.

- Multi-tenant isolation is enforced using a nullable `OrganizationId` column on tenant-scoped tables.
- A `null` value indicates "community scope", representing independent seniors and volunteers.
- EF Core global query filters automatically apply isolation filters based on the authenticated user's `OrganizationId` retrieved from the JWT token.
- Platform-level scopes (e.g. background jobs, administrators) can bypass these filters dynamically.

## Consequences

- Low operational database costs and easy schema migrations.
- Tenant isolation is handled transparently at the database querying layer (using EF Core global filters), reducing the risk of data leakage.
- Allows seamless switching/coexistence between organization-managed and community-managed activities.
