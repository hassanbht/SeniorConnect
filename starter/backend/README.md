# SeniorConnect Backend — skeleton

> ⚠️ **This code has not been compiled.** No .NET SDK was available in the
> environment where it was written. The structure, the rules and the tests are
> the deliverable; expect to fix missing `using` directives, package versions
> and a handful of signatures on first build. The SQL in `../sql/` *has* been
> executed and verified against PostgreSQL 16.15.

Target: **.NET 10** (`net10.0`). Nothing here depends on a .NET-10-specific API,
so it should build on .NET 8+ with a `TargetFramework` change.

## Layout

```
Directory.Build.props            nullable, warnings-as-errors, analysis level

src/
  SeniorConnect.SharedKernel/         Result · Error · Entity · DataClass · IFunderVisible
  SeniorConnect.Infrastructure/       DbContext, global query filters, adapters
  SeniorConnect.Api/                  composition root, endpoints, middleware

modules/
  Activities/                    the Phase 2 wedge, complete vertical slice
    Domain/                      Activity aggregate — invariants live here
    Application/                 use cases, the authorization pipeline
    Contracts/                   the ONLY thing other modules may reference
    Infrastructure/              EF configuration
    Endpoints/
  Funder/
    Contracts/                   aggregate-only DTOs + suppression

tests/
  SeniorConnect.ArchitectureTests/    the rules, mechanically enforced
```

## Why the architecture tests matter more than usual here

Four decisions in this product are one careless refactor away from becoming a
data breach, and none of them are visible in a code review of the diff that
breaks them:

| Test file | Protects |
| --- | --- |
| `TenantIsolationTests` | Org A cannot read Org B (BR-TENANT-03) — **and** community rows with a null organization stay visible (ADR-007) |
| `FunderApiSurfaceTests` | A Gemeinde can never reach a person's name (BR-FUNDER-02), walking the whole DTO object graph |
| `SafeguardingIsolationTests` | Safeguarding data never reaches Reporting, Funder or Notifications (BR-SG-05) |
| `EndpointSecurityTests` | No client-supplied trust level, safety level or capability (authorization.md §6); every persisted property is data-classified |
| `ModuleBoundaryTests` | Cross-module access goes through `Contracts` only (ADR-001) |

`TenantIsolationTests.Query_filters_admit_rows_with_a_null_organization` is
worth reading closely. A filter of the shape
`x.OrganizationId == tenant.OrganizationId` compiles, passes a naive "does it
have a filter" check, and silently hides every community activity in the
system — quietly killing the org-optional thesis. That test is the only thing
standing between ADR-007 and a plausible-looking one-line change.

## Two guards, on purpose

BR-TRANSPORT-04 (a private-vehicle activity cannot be confirmed while the
insurance context is unknown) is enforced **twice**:

1. `Activity.Confirm()` — the primary guard, returns a typed `Error`
2. `ck_activities_transport_insurance` — the database backstop

That is deliberate duplication, not an oversight. F4 records a real insurance
dispute between a municipality and an association after a volunteer's car
accident. A raw SQL fix-up script or a future bulk import will not go through
the domain layer.

## First build

```bash
dotnet new sln -n SeniorConnect
# add the projects, then:
dotnet restore
dotnet build
dotnet test tests/SeniorConnect.ArchitectureTests
```

The architecture tests should pass on an almost-empty solution — they are
written to hold from the first commit, which is the point.
