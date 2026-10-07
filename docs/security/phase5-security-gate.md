# Phase 5A / 5B Security Gate

> Status: 2026-09-28 (Phase 5B). Evidence record for the Phase 4/5 gate. Every
> claim cites code or tests; nothing here changes behavior. Phase 5B findings
> are in §10–§14; §1–§9 are the Phase 5A record and still hold.

## 1. PostgreSQL integration isolation (was: 2 failing tests)

**Root cause.** `Phase4PostgresTests` and `LocationConsentPostgresTests` both
called `MigrateAsync()` on whatever database `SC_TEST_POSTGRES` named — in
practice the developer's docker-compose database with existing state. xUnit
runs test classes in parallel, so two migrators raced on one shared schema
(`42701: column "LastMonthlyReminderMonth" already exists`). Sequential runs
passed; parallel runs failed or, worse, passed only because the database was
already migrated — masking whether the migration chain actually replays from
empty.

**Fix.** `tests/backend/SeniorConnect.Modules.HelpRequests.Tests/PostgresTestDatabase.cs`:
an `IClassFixture` that creates a uniquely named database
(`sc_test_<12 hex>`), applies the migration chain to it once, hands its
connection string to the class, and drops the database on dispose
(`DROP DATABASE ... WITH (FORCE)`, PostgreSQL 13+). Each class owns an empty,
freshly migrated database:

```
TEST START -> create empty database (unique name)
           -> apply migrations deterministically, once
TESTS      -> run against this known state
DISPOSE    -> drop database
```

No skips were added; facts still skip only when `SC_TEST_POSTGRES` is unset.
`SC_TEST_POSTGRES` must point at a disposable instance (the docker-compose db
qualifies); the database it names is never touched. Verified leftover count
after runs: **0**.

**Migration chain verification.** New
`MigrationRoundTripPostgresTests.Migration_Chain_RoundTrips_DownAndUp` runs the
full chain **down to Phase 1 and up again** on its own empty database, then
asserts the Phase 4 constraints still hold. Note found and documented in the
test: historical down-migrations re-add *validated* check constraints, so a
down/up replay is only sound on an empty database — which is exactly what the
fixture guarantees. Also confirmed: no PostGIS/extension dependency exists in
the backend (the dev image ships PostGIS, nothing uses it; CI uses plain
postgres:16).

**Existing-data compatibility.** The Phase 4 migration adds only new tables
(`discovery_preferences`, `location_sharing_consents`, `verifications.method`,
`user_blocks` unique pair, `safeguarding.restrictions`), widens
`ck_verifications_status` to include `Revoked`, and imposes defaults on new
columns only (`is_discoverable false`, consent level `NONE`). No NOT NULL on
pre-existing rows, no backfill conflict. BR-DISC-01 ("no existing user became
discoverable") holds by construction.

**CI.** `.github/workflows/ci.yml` now starts a PostgreSQL 16 service
container and exports `SC_TEST_POSTGRES`, so the integration gate runs on
every push/PR instead of skipping.

**Result: PASS.** Suite: 577 backend + 54 architecture tests, 0 failed,
0 skipped, including 3 real-PostgreSQL tests.

## 2. Security regression (all previous suites re-run)

| Area | Evidence | Result |
| --- | --- | --- |
| Tenant isolation | `ModuleBoundaryTests`, BR-TENANT-03 architecture test | PASS |
| Matching authorization | `TrustLevelComputationTests`, `MatchingService` invariant (BR-SAFETY-03) | PASS |
| Location privacy / no lat-lon probing | `LocationLeakSweepTests`, `LocationVisibilityArchitectureTests` | PASS |
| Consent enforcement | `LocationConsentAndVisibilityTests`, `LocationConsentPostgresTests` | PASS |
| Discovery eligibility | `Phase4VerificationDiscoveryTests` | PASS |
| Block enforcement | block + live-assignment withholding tests | PASS |
| Safeguarding enforcement | `SafeguardingIsolationTests`, `SafeguardingLeakSweepTests` | PASS |
| Verification integrity | provenance fields, revocation, `ck_verifications_*` DB checks | PASS |
| No fake data / fake geocoder | `GeocodingProviderTests` (no fabricated coordinates) | PASS |

## 3. Client trust boundary (Phase 4 §17)

Audited every Phase 4 write path. The Flutter app cannot make the server
accept: `verificationLevel` / `trustLevel` / `safetyLevel` /
`organizationVerified` / `addressVerified` / another user's `discoverable` /
safeguarding status / location authorization.

- `PUT /api/v1/me/discoverability` — acts only on `user.GetUserId()`; the
  body carries only the caller's own boolean. (VerifiedEndpoints.cs)
- `PUT /api/v1/me/location-sharing` — same; level validated server-side
  against the `PRECISE/APPROXIMATE/NONE` check constraint.
- Verification facts: `POST /organizations/{orgId}/volunteers/{userId}/verifications`
  — server resolves type/method from an allow-list, requires
  `EnsureStaffAsync(orgId)` and the subject's active roster membership; no
  client-supplied status or level exists in the contract.
- Trust level: computed in `TrustLevelCalculator` from persisted facts;
  `PerformSafetyLevelN` capabilities are never hand-grantable (P4-04 filter in
  `CapabilityService`).
- Safeguarding: raise/close/restrict gated on the `SafeguardingOfficer`
  capability server-side; the restriction tenant comes from the case row,
  never the request body.

## 4. Restriction-bypass audit (Phase 4 §12)

Consumers of the four restriction kinds — all read fresh on every request
(`SafeguardingRestrictionReader`), none cache:

| Restriction | Discovery | Location | Contact | Assignment | Coordinator | Admin | Officer | Cross-tenant |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DISCOVERY_BLOCKED | withheld | — | — | — | no bypass | no bypass | no bypass (consent still applies) | 404/filtered |
| LOCATION_BLOCKED | — | `LocationDisclosure.None` | — | — | no bypass | no bypass | no bypass | 404/filtered |
| CONTACT_BLOCKED | — | — | BR-COMM-04 data withheld, incl. during live assignment | — | no bypass | no bypass | no bypass | 404/filtered |
| ASSIGNMENT_BLOCKED | — | — | — | candidate filtered from matching | no bypass | no bypass | no bypass | 404/filtered |

Changing role or organization does not lift a restriction (the subject is the
restricted user, not the role); restrictions are pair-scoped when
`CounterpartUserId` is set, global otherwise; endpoint-shuffling does not help
because every consumer (discovery, matching, geography proximity, help-request
mapping) funnels through the same two readers. Officers are **not** exempt
from consent (BR-DISC-06); imposing/lifting requires a visible case, and every
action is audited (BR-SG-06). Blocks behave identically (BR-DISC-05) and are
checked in the same readers.

## 5. API contract audit (Phase 4 §16)

Phase 4/5A endpoints (all `RequireAuthorization`; problems use the standard
ProblemDetails with machine-readable `code`):

| Method & route | Auth | Scope | Sensitive data | Error codes | Audit |
| --- | --- | --- | --- | --- | --- |
| `GET /api/v1/me/verification` | caller only | own facts | badge facts + org name; no verifier identity, no documents | 401 | via badges only |
| `GET /api/v1/me/discoverability` | caller only | own switch | boolean + timestamp | 401 | — |
| `PUT /api/v1/me/discoverability` | caller only | own switch | boolean | 401 | — |
| `POST /api/v1/organizations/{orgId}/volunteers/{userId}/verifications` | org staff | own org + active roster | recorded fact (type, method, validity) | `VERIFICATION_TYPE_INVALID`, `VERIFICATION_METHOD_INVALID`, 403, 404 (`VERIFICATION_NOT_A_MEMBER`) | entity audit |
| `POST /api/v1/organizations/{orgId}/verifications/{id}:revoke` | org staff | own org's facts | — | 403, 404, 409 | entity audit |
| `GET /api/v1/me/location-sharing`, `PUT …` | caller only | own consent | level + version | 401, 400 (`LOCATION_LEVEL_INVALID`) | consent history rows |
| `POST /api/v1/trust-safety/blocks`, `GET`, `DELETE /blocks/{userId}` | caller only | own blocks; 404 never confirms a reverse block | reason stored, never shown to the blocked party | 400 (self-block), 404 | entity audit |
| `POST /api/v1/safeguarding/concerns` | any authenticated | subject/reporter | reporter identity stored, never shown to subject (BR-SG-04) | 400 | `SafeguardingAccessLog` |
| `GET /safeguarding/cases`, `GET /cases/{id}`, `POST /cases/{id}/notes`, `POST /cases/{id}:assign`, `POST /cases/{id}:close` | officer only (BR-SG-02) | own org's + org-less + assigned cases | case data never in non-officer surfaces (BR-SG-05) | 403 `SAFEGUARDING_OFFICER_REQUIRED`, 404 | per-read access log |
| `GET /cases/{id}/restrictions`, `POST /cases/{id}/restrictions`, `POST /restrictions/{id}:lift` | officer only | case-bound; tenant from case row | only which restrictions apply, never the case reason, leak-checked by architecture tests | 403, 404, 409 | per-action access log |

No Phase 4 endpoint exposes exact coordinates, hidden verification facts
(verifier identity beyond org name), safeguarding case details, or who-blocked-
whom without the corresponding authorization. Discovery and proximity
endpoints disclose at most locality + approximate distance per BR-GEO-06.

## 6. Safeguarding case lifecycle (Phase 4 §13)

States that exist in code (`SafeguardingStatus`): `Open, Assigned,
UnderInvestigation, ActionTaken, Resolved, Closed`. Transitions implemented:
Raise→Open; `:assign`→Assigned (records officer); notes at any point;
RecordAction→ActionTaken; `:close`→Closed (resolution notes, timestamp,
audit). `UnderInvestigation` and `Resolved` exist in the enum but have no
transition today. Who is notified when a restriction is imposed: **nobody
(automatic)** — see decision S-2 in the decision matrix. Who may resolve/lift:
only an officer who can see the case; lifting is audited with a reason. What
happens after resolution: restrictions stay active until explicitly lifted
(case closure does not auto-lift) — documented as decision S-2 scope.

## 7. Block behavior (Phase 4 §14)

Current: a block removes the pair from discovery (both directions), forces
`LocationDisclosure.None`, withholds BR-COMM-04 address/phone/exact
coordinates (including during a live assignment), and filters both from
matching. It does **not** unassign, cancel proposals, or delete conversations;
only the blocker can lift (404 semantics avoid confirming reverse blocks).
Desired-state discussion and the coordinator-flag middle option: decision
B-1. The security requirement is met in all states.

## 8. Report escalation (Phase 4 §15)

`UnsafeBehavior` community reports are flagged
`EscalatedToSafeguarding` on the report; no case is auto-created and no
officer is auto-notified (decision B-2, with options). No escalation rule
beyond the flag exists in the domain; nothing speculative was built.

## 9. Residual risks (accepted for the current deployment, each with an owner)

1. **Visit-address HIGH residual** (ADR-026): BR-COMM-04 delivers the typed
   address to the assigned volunteer without per-visit consent while the
   decision is open. Owner: product + legal.
2. **Org-less safeguarding cases are visible to every officer of any
   organization** (S-4). Acceptable for a single organization; must be revisited before a
   second organization joins. Owner: product.
3. **Escalated community reports have no officer surface** (B-2): the flag can
   rot unseen. Owner: product + safeguarding lead.
4. **No automatic notification on restriction imposition** (S-2): officers
   must watch their queue. Owner: product + safeguarding lead.
5. **Trust-level documentation drift** (V-3/ADR-027): RESOLVED 2026-09-30 (ADR-027 ratified, Option A); before ratification, readers
   must use ADR-027 to interpret the numeric levels. Owner: product owner.

---

# Phase 5B — Policy closure

## 10. Decision inventory reconciliation

The Phase 5A report said "eleven pending decisions". Its own status block listed
**eleven tracked rows**, one of which (`V-4`) was already RESOLVED. Canonical:
**11 tracked items** (10 decision IDs + the visit-address ADR), **10 pending
after 5A**, **1 resolved**. There is **no eleventh open decision**. Corrected in
`docs/product/phase5-decision-matrix.md` §0; no decision ID was invented.

## 11. Classification (brief §2)

| Class | Items |
| --- | --- |
| **A — rule determined, implemented** | V-1, V-2, V-4, S-1, B-1 effects |
| **B — technical interpretation** | V-3 separation (no behaviour change) |
| **C — product decision required** | S-2, S-3, S-4, B-1 unassignment, B-2 |
| **D — legal/compliance** | ADR-026 |
| **E — implementation bug** | **none found** |

No rule-determined behaviour was left unimplemented and no bug against an
existing rule was found. Details per item: `docs/product/phase5-decision-matrix.md`.

## 12. Operational safety improvements (brief §12) — all already in place

| Requirement | Evidence | Result |
| --- | --- | --- |
| A/B — restriction checks centralized; every path consumes the same policy | `SafeguardingRestrictionReader` is the single reader; discovery (`DiscoveryEligibilityService`), location (`LocationVisibilityService`), contact (`HelpRequestService.IsContactWithheldAsync`) and assignment (matching) all call it fresh | PASS |
| C — cancellation/unassignment removes protected-location access | ADR-026 hardening: `IsLiveAssignee` gates address/phone/coordinates to `Assigned`/`InProgress`; `T36_T38_ScenarioF_CancellationEndsAccess` | PASS |
| D — verification revocation immediately affects eligibility | `VerificationFactsReader` filters `Status == Verified` and unexpired on every call; `T08_ARevokedFact_StopsCountingImmediately` | PASS |
| E — block state evaluated server-side in every relevant endpoint | `SafetyBoundaryReader` used by discovery, matching, location and help-request mapping; no client-supplied block/origin input (`LocationLeakSweepTests`) | PASS |
| F — tenant scope on every organization-scoped safeguarding record | `VisibleCasesAsync`; new `docs/security/safeguarding-scoping-policy.md`; `Phase5PolicyClosureTests.S4_*` + `T30`/`TenantMatrix` | PASS |
| G — audit events for revocation / discoverability / consent / restriction / block / report | see §13 | PASS (one nuance documented) |

**Audit nuance (G).** Verification revocation, discoverability changes and
location-consent changes are written through `SeniorConnectDbContext`, which
runs the `AuditInterceptor` (`CREATE_DISCOVERYPREFERENCE`,
`CREATE_LOCATIONSHARINGCONSENT`, `CREATE_VERIFICATION`, `UPDATE_VERIFICATION`).
Blocks (`CREATE_USERBLOCK`) and community reports (`CREATE_COMMUNITYREPORT`) are
recorded the same way. Safeguarding is deliberately different:
`SafeguardingDbContext` has **no** generic interceptor, so case text never
reaches `audit_entries`; the module writes its own append-only
`SafeguardingAccessLog` per action instead (BR-SG-06, BR-SG-05). New test:
`Phase5PolicyClosureTests.Audit_DiscoverabilityConsentVerificationAndBlock_AreAllRecorded`.

## 13. Safeguarding scoping (brief §9)

- Organization-scoped cases are strictly tenant-scoped: an officer of another
  organization gets 404, and a cross-tenant restriction cannot be imposed (the
  tenant comes from the case row, never the request body).
- Org-less cases are platform-level today (every officer sees them) — the
  deliberate single-organization trade-off of decision **S-4**, now written down as the
  `PLATFORM_LEVEL` vs `ORGANIZATION_SCOPED` boundary in
  `docs/security/safeguarding-scoping-policy.md`.
- An ordinary `OrganizationAdmin` has no officer scope: the service returns no
  organization case to them and the endpoint refuses with
  `403 SAFEGUARDING_OFFICER_REQUIRED` (BR-SG-02). The capability check lives at
  the endpoint; the service enforces tenant scope. That split is documented as
  the defense-in-depth limit of the design (single caller today: the guarded
  endpoint).

## 13b. API security sweep re-run (brief §13)

Repeated over all Phase 4/5 code. Result: **no request contract carries
client-authoritative protected state.**

| Searched for | Where it appears | Verdict |
| --- | --- | --- |
| `VerificationLevel` | no request record | not accepted from a client |
| `TrustLevel` | no request record | computed in `TrustLevelCalculator` |
| `SafetyLevel` | no request record | derived by `IActivitySafetyPolicy` from category + context |
| `OrganizationVerified`, `AddressVerified` | no request record | recorded only by org staff (V-4) |
| safeguarding status/kind | no request record | officer endpoints derive everything from the case row |
| `Discoverable` | `UpdateDiscoverabilityRequest(bool)` only | acts on `user.GetUserId()`, never on another user |
| `Latitude`/`Longitude` | caller-own-data writes only: support/volunteer profile, own help request, org/event venue | validated by `GeoCoordinate.IsValidPair`, never a cross-user discovery origin (`LocationLeakSweepTests`); `LocationVisibilityPolicy` shapes every response |
| `OrganizationId`/`TenantId` on safeguarding requests | `RaiseConcernRequest.OrganizationId` only | chosen by the reporter at raise time; an officer's *restriction* tenant comes from the case row, never the body |

Tenant scope, ownership checks and the `404`-not-`403` cross-tenant convention are
unchanged from §4/§5 and remain covered by `T30`, `TenantMatrix`,
`TrustSafetyAuthorizationTests` and `ModuleBoundaryTests`.

## 14. Policy tests added in 5B (brief §18)

| Brief §18 item | Test |
| --- | --- |
| 3 — V-3 trust/verification stay separate | `Phase5PolicyClosureTests.V3_*` (2) + `Phase5PolicySweepTests.VerificationFacts_DoNotCarryATrustOrSafetyLevel` |
| 5–9 — each restriction is server-enforced | pre-existing `T13/T27`, `T28`, `T37`, `AnAssignmentRestriction_BlocksAccepting`; new `S-1` test adds the officer case with `PRECISE` consent |
| 10 — cross-tenant restriction cannot be manipulated | `T30`, `TenantMatrix`, new `S4_*` |
| 11 — ordinary admins cannot reach safeguarding details | `OrganizationCoordinatorReaderTests` (admin implies no officer scope), `TrustSafetyAuthorizationTests`, new `S4_*` (admin gets no organization case) |
| 15 — active assignment matches the documented B-1 rule | new `B1_A_Block_WithholdsTheVisitAddress_ButNeverSilentlyUnassigns` |
| 16–17 — report audited, details not leaked | `T25_T26`, new `B2_*` |
| 18–20 — visit-address behaviour unchanged; option C not activated | `T34`/`T35`/`T36_T38`, new `VisitAddress_ADR026IsProposed_*`, `Phase5PolicySweepTests.NoVisitAddressGrantArtifact_*` |
| BR-AUDIT-01 | new `Audit_DiscoverabilityConsentVerificationAndBlock_AreAllRecorded` |

All values that phase 5A already pinned (V-1, V-2, V-4, tenant isolation) keep
their existing tests; no assertion was weakened, no test was skipped.

## 15. Phase 5B results

| Gate | Result |
| --- | --- |
| Backend tests | PASS — 0 failed, 0 skipped, incl. 8 new policy tests |
| Architecture tests | PASS — incl. 2 new sweep tests |
| PostgreSQL integration | PASS — per-class empty databases (unchanged strategy) |
| Flutter analyse + tests | PASS |
| Security regression | PASS — no previous Critical/High finding returned |
| Working tree hygiene | PASS — new files only; no formatting-only edits |

## 16. Residual risks after 5B — status 2026-09-30 (approved policy implementation)

1. **Visit-address HIGH residual** (ADR-026, class D) — **engineering side
   closed, legal side open.** Option C is implemented behind
   `VisitAddress:RequireGrant` (ships **false**; an architecture guard keeps it
   off in every committed config). The residual remains until the Legal/Data
   Protection Gate passes and an operator enables the gate — legal review
   pending, no compliance claimed.
2. **Org-less safeguarding cases visible to every officer** (S-4) — **RESOLVED
   2026-09-30 (Option B implemented):** org-less cases are visible only to the
   explicit `PlatformSafeguardingOfficer` role; organization scoping is
   unchanged. Hard requirement for a second organization is met.
3. **Escalated community reports have no officer surface** (B-2) — **RESOLVED
   2026-09-30 (Option A implemented):** officer-only manual conversion
   (`POST /community/reports/{id}:convert-to-case`) with report-to-case
   traceability, duplicate protection (409) and no automatic creation.
4. **No automatic notification when a restriction is imposed** (S-2) —
   **RESOLVED 2026-09-30 (Option B implemented):** a content-free operational
   task is created for coordinator surfaces; no safeguarding detail crosses the
   boundary (BR-SG-05), by design and by leak-sweep test.
5. **Trust-level documentation drift** — RESOLVED: ADR-027 ratified 2026-09-30
   (V-3) — product owner.
6. **A block never unassigns** (B-1) — **RESOLVED 2026-09-30 (Option B
   implemented):** a newly created block releases the pair's live assignment(s)
   deterministically and idempotently (status → Open, audited status-history,
   no reason leak). Withholding protections remain active in all states.

No new Critical or High finding was introduced in Phase 5B. No Product or Legal
decision was invented: all seven approvals come from the owner decision pack
(2026-09-30); the only open gate is the external legal review for ADR-026.
