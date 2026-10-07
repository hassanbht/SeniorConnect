# Safeguarding case scoping policy (PLATFORM_LEVEL vs ORGANIZATION_SCOPED)

> Phase 5B, 2026-09-28; revised 2026-09-30. Status of the boundary itself:
> **DEFINED and enforced**. Status of the org-less owner question (decision
> **S-4**): **RESOLVED 2026-09-30 (Option B, approved & implemented)** — the
> explicit `PlatformSafeguardingOfficer` role exclusively owns org-less
> (PLATFORM_LEVEL) cases; ordinary organization officers no longer see them.
> See §5 and `docs/product/phase5-decision-matrix.md`.

## 1. Why a boundary is needed

Two rules collide when a case has no organization:

- **BR-SG-02** — access requires the explicit `SafeguardingOfficer` capability;
  `OrganizationAdmin` does **not** imply it. Organization A can never read
  organization B's data (BR-TENANT-03).
- **ADR-007** — the platform is organization-optional: a case may legitimately
  have `OrganizationId == null` (a neighbourly concern raised outside any
  organization).

BR-TENANT-03 forces organization-attached cases to be tenant-scoped.
ADR-007 makes org-less cases possible. Neither rule says **who** owns an org-less
case, so a boundary has to be named before it can be decided.

## 2. The two scopes

| Scope | Applies to | Who may see it | Who may act on it |
| --- | --- | --- | --- |
| **ORGANIZATION_SCOPED** | `SafeguardingCase.OrganizationId != null` | only an active `SafeguardingOfficer` **of that organization**, or the assigned officer | the same |
| **PLATFORM_LEVEL** | `SafeguardingCase.OrganizationId == null` | every active `SafeguardingOfficer` of any organization (current single-organization behaviour) | the same |

## 3. Enforcement (current implementation, revised 2026-09-30)

`SafeguardingService.VisibleCasesAsync` is the single visibility rule:

```
orgIds.Contains(case.OrganizationId)              // organization-scoped
|| case.AssignedOfficerUserId == officerUserId    // explicit assignment
```

and, only when the caller holds the explicit **`PlatformSafeguardingOfficer`
role** (`IOrganizationCoordinatorReader.GetPlatformSafeguardingOfficerUserIdsAsync`):

```
case.OrganizationId == null                       // platform-level
|| orgIds.Contains(case.OrganizationId)
|| case.AssignedOfficerUserId == officerUserId
```

where `orgIds` comes from
`IOrganizationCoordinatorReader.GetSafeguardingOfficerOrganizationIdsAsync`,
which returns only organizations where the caller holds an **active
`SafeguardingOfficer` membership**. Coordinator and Admin memberships are
ignored (BR-SG-02). An ordinary organization officer therefore no longer sees
org-less cases (this supersedes the single-organization behaviour documented in
the 2026-09-28 revision of this document).

Consequences, all covered by tests:

- An officer of organization B gets **404** for an organization-A case
  (`TrustSafetyAuthorizationTests`,
  `Phase5PolicyClosureTests.S4_*`).
- A restriction can only be imposed on a case the officer may see; the tenant is
  read from the **case row**, never from the request body
  (`ImposeRestrictionAsync`). A cross-tenant restriction is refused.
- An ordinary `OrganizationAdmin` has no officer scope at all: the service
  returns no organization case to them, and the endpoint refuses the request
  outright with `403 SAFEGUARDING_OFFICER_REQUIRED`
  (`SafeguardingEndpoints.IsSafeguardingOfficer`).
- Every read and every action writes a `SafeguardingAccessLog` (BR-SG-06).

### Defense in depth, and its limit

The **capability check lives at the endpoint** (`IsSafeguardingOfficer`); the
service layer enforces the **tenant scope**. A caller reaching
`ISafeguardingService` directly would still be scoped by organization but would
see org-less cases regardless of capability. Today the only caller is the
officer-guarded endpoint, so this is not exploitable — but it is the reason the
endpoint check must never be removed. Adding a second capability check inside
the TrustSafety module would require a dependency on the Identity module's
capability service and is not justified by the current call graph.

## 4. Safeguarding data never reaches ordinary surfaces

- `SafeguardingDbContext` is a **separate** DbContext from
  `SeniorConnectDbContext`. Case data is therefore not written through the
  generic `AuditInterceptor`; the module keeps its own append-only
  `SafeguardingAccessLog` instead, so case text never lands in `audit_entries`.
- `SafeguardingIsolationTests` and `SafeguardingLeakSweepTests` fail the build
  if any other module gains a dependency on safeguarding types (BR-SG-05).
- Only the four restriction *kinds* cross the module boundary
  (`ActiveRestriction`); no case id, reason, officer or severity does.

## 5. The org-less owner question (S-4) — RESOLVED 2026-09-30

> Who owns a safeguarding case that has no organization?

**Resolved (owner decision, 2026-09-30): option 2 — the explicit
`PlatformSafeguardingOfficer` role exclusively owns org-less (PLATFORM_LEVEL)
cases.** Implemented in this phase:

- **Role:** the platform officer is a named user id held by
  `IOrganizationCoordinatorReader.GetPlatformSafeguardingOfficerUserIdsAsync`
  (production implementation reads the dedicated role assignment store; tests
  inject it). It is **not** implied by any organization membership or admin
  role.
- **Appointment / removal:** `POST /api/v1/safeguarding/platform-officers` and
  `DELETE .../{userId}` — officer-gated, audited management endpoints.
- **Visibility:** the rule in §3; ordinary organization officers see org-less
  cases no longer.
- **Lifecycle / emergency ownership failure:** if no platform officer is
  appointed, org-less cases remain stored and audited but visible to nobody
  except an explicitly assigned officer — fail-closed, never fail-open to
  organization officers. Re-appointment restores access.
- **Tests:** `Phase5PolicyClosureTests.S4_*` (tenant scoping + explicit role),
  `TrustSafetyAuthorizationTests` (an ordinary officer lists only own-org
  cases), platform-officer endpoint authorization tests.

The former option 1 (every officer of every organization sees org-less cases)
stayed in force until 2026-09-30 and is retained above as the historical
single-organization behaviour; option 3 (require an organization on every case)
remains rejected — it contradicts ADR-007 and BR-TENANT-02.
