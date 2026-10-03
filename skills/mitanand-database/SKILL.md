---
name: SeniorConnect-database
description: Use when creating or changing PostgreSQL schema, EF Core entities, migrations, query filters or indexes in SeniorConnect.
---

# SeniorConnect Database

## Conventions

```
Primary keys      uuid v7 (time-ordered — good index locality)
Naming            snake_case tables and columns, plural table names
Timestamps        UTC only, named *_at_utc, stored as timestamptz
Audit columns     created_at_utc, created_by, updated_at_utc, updated_by
Concurrency       xmin as the EF concurrency token
Soft delete       is_deleted + global query filter, for user content only
Tenant            organization_id, NULLABLE where community scope is valid
Money             none (there is no money in this product)
Enums             stored as text with a check constraint, not as a Postgres enum type
                  (Postgres enums are painful to alter)
```

## Before writing a migration

```
1. Which module owns this table?
2. Is the organization_id nullable? (it usually should be — BR-TENANT-02)
3. Does it need a global query filter? (tenant, soft delete, or both)
4. Which data class is each column? (see privacy-gdpr.md §1)
5. Is any column health-related or sensitive? Can it be avoided entirely?
6. Which indexes does the actual query pattern need?
7. Is it PostGIS-ready? (lat/lng as double precision, so a generated geography column
   can be added later without a data migration)
```

## Migration rules

```
✓ One migration per logical change, named descriptively
✓ Reversible where possible
✓ Backfill data in a separate, idempotent step — never inside the schema migration
✓ Test the migration against a seeded copy before it goes near production
✗ Manual DDL in any environment
✗ Dropping a column in the same release that stops writing to it
  (two releases: stop writing → verify → drop)
✗ Database triggers without an ADR
✗ Business logic in SQL — filtering is fine, scoring and policy are not
```

## Query filters — always verify

```csharp
modelBuilder.Entity<HelpRequest>()
    .HasQueryFilter(e => !e.IsDeleted
        && (e.OrganizationId == null || e.OrganizationId == _tenant.OrganizationId));
```

An architecture test asserts that every entity implementing `IOrganizationScoped` has a
filter. If you add such an entity without a filter, the build fails. Do not remove that test.

## Safeguarding schema

Lives in the separate PostgreSQL schema `safeguarding`, with its own DbContext and its own
DB role grants. Never join to it from an ordinary query. Never include it in a generic
export.

## Indexing

Add the index in the same migration as the query that needs it. Prefer partial indexes:

```sql
CREATE INDEX ix_help_requests_open
  ON help_requests (requested_start_utc)
  WHERE status IN ('open','matching');
```

Before merging a new list endpoint, run `EXPLAIN ANALYZE` against a seeded dataset of at
least 50 000 rows and paste the plan into the PR.

## PostGIS path (Phase 8)

Design so that this is purely additive:

```sql
CREATE EXTENSION postgis;
ALTER TABLE senior_profiles ADD COLUMN geo geography(Point,4326)
  GENERATED ALWAYS AS (ST_MakePoint(longitude, latitude)::geography) STORED;
CREATE INDEX ix_senior_geo ON senior_profiles USING GIST (geo);
```

Until then, Haversine in a SQL expression is correct and fast enough.
