# Backend Rules

Read the root `AGENTS.md` first. These rules are additional, not a replacement.

## Architecture

Modular monolith. Each module owns its Domain, Application, Infrastructure and Endpoints.

```
A module may reference ONLY another module's Contracts project.
Never Domain, never Application, never Infrastructure.
Cross-module communication: Contracts interfaces or domain events.
Each module owns its tables. Only users.id may be referenced across modules.
```

Enforced by architecture tests in `../tests/architecture/`. If you need to break a rule,
write an ADR first.

## Layering

```
Endpoints      authenticate → authorize → validate → call application → map → return
Application    use cases, orchestration, authorization decisions, transaction boundary
Domain         entities, value objects, invariants, state machines. No EF. No DTOs. No IO.
Infrastructure EF configurations, repositories, external adapters
```

Business logic in an endpoint is a defect.
A domain entity that references `DbContext` is a defect.

## API

- DTOs only. Never expose an EF entity.
- FluentValidation on every request.
- RFC 7807 `ProblemDetails` with a stable `code` on every error.
- Cursor pagination on every list.
- `Idempotency-Key` honoured on every state-changing POST/PUT/PATCH.
- Before adding an endpoint, check whether an existing one can be extended.

## Security

Never trust from the client:

```
userId · role · capabilities · trustLevel · safetyLevel
organizationId (when derivable from membership)
requiredTrustLevel · matchScore · isVerified
```

- Authorization is policy + capability based and always evaluated server-side.
- Cross-tenant misses return **404**, not 403.
- Every endpoint has four tests: 401 unauthenticated, 403 without capability,
  404 cross-tenant, 200 happy path. Missing negative tests block the merge.

## Database

```
uuid v7 primary keys · snake_case · UTC timestamps named *_at_utc
created_at_utc, created_by, updated_at_utc, updated_by on every important entity
xmin as the EF concurrency token
Nullable organization_id where community scope is valid
EF global query filters for tenant isolation and soft delete
Migrations for every schema change. No manual production DDL.
No database triggers without an ADR.
No stored business logic in SQL — filtering in SQL is fine, scoring is not.
```

## Concurrency

Use a conditional UPDATE for assignment-style operations:

```sql
UPDATE help_requests
   SET assigned_volunteer_id = @v, status = 'assigned'
 WHERE id = @id AND status = 'offered';
```
`RowsAffected = 0` → 409 with a reason code. Do not rely on optimistic concurrency alone
for the assignment race.

## Testing

```
Unit         every business rule, every state transition, every safety policy branch
Integration  help request creation, matching, acceptance, family access,
             trust computation, safeguarding access, tenant isolation
Architecture module boundaries, query filters present, no entity leaks in DTO assemblies
```

Use Testcontainers for PostgreSQL integration tests. Do not mock the database.

## Logging

Structured (Serilog), correlation ID on every request, **never** personal data in a log
line. IP addresses stored only as a salted hash.
