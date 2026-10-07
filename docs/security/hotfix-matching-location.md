# Security Hotfix — Matching and Location Leaks

> Date: 2026-09-25 · Status: **fixed and tested; not yet deployed or committed**
> Source audit: [`location-and-reference-data-design.md`](location-and-reference-data-design.md) §1.3 (L1–L5, L7, L8)
> Scope: this hotfix only closes the active leaks. The consent model, verification
> facts, Drift, the ETag cache, the geocoder and all schema changes stay in the
> Phase-2 design.

## 1. Vulnerabilities

| ID | Severity | Endpoint | What leaked |
|---|---|---|---|
| L1 | CRITICAL | `GET /api/v1/matching/requests/{id}/candidates` | Any authenticated user could query any help request, in any tenant. The response held each accepting volunteer's user id and the distance to 0.1 km, computed from the exact home coordinate. The same number also appeared in the ineligibility reasons and explanation text. |
| L2 | CRITICAL | `POST /api/v1/matching/proposals:hybrid` | The same data as L1, plus completion history |
| L3 | HIGH | `GET /api/v1/matching/feed?latitude&longitude` | The caller chose the origin and received distances to 0.1 km from the exact request coordinates (usually a senior's home). Requests outside the radius were listed as well. |
| L4 | MEDIUM-HIGH | `GET /api/v1/discovery/nearby-volunteers?latitude&longitude` | The caller chose the origin and received each volunteer's grid cell plus distances in 0.5 km steps. Repeating this could sweep all of Austria. |
| L5 | MEDIUM-HIGH | `GET /api/v1/discovery/nearby-requests?latitude&longitude` | The same as L4, for open requests |
| L7 | MEDIUM | Community group endpoints (`CommunityGroupDto`) | The exact meeting-place coordinates, which can be a private home |
| L8 | MEDIUM | All of the above | The caller's own home coordinates sat in the query string, where access logs and request-line logging can see them |

**L1 attack.** The attacker creates three help requests at points of their choice;
`POST /help-requests` accepts client latitude/longitude. They then call candidates
for each request and intersect the three distance circles. The result is each
volunteer's home to within about 100 m, tied to a user id.

## 2. Root cause

- **No authorization on the matching endpoints.** The group had `.RequireAuthorization()`, which only checks authentication. Nothing checked the caller's relationship to the request or to its tenant.
- **Caller-controlled origin.** The feed and discovery endpoints used `latitude`/`longitude` from the query string as the search origin, so any point could be probed.
- **Exact distances to private places.** Distances were computed from exact coordinates and returned in km. BR-GEO-06 fuzzing covered the coordinate fields but not the distances.
- **Unmasked group DTO.** `CommunityGroupDto` mapped the stored coordinates 1:1.

## 3. Fix

### 3.1 Authorization rule (candidates and hybrid proposals)

`MatchingService.EnsureCallerMayMatchAsync` is the single rule for every
human-facing matching view:

| Caller | Result |
|---|---|
| Unauthenticated | **401** (at the endpoint) |
| Request unknown or deleted | **404** |
| Request has no organization (community request) | **404**. Only the automatic tier dispatcher matches these; no human sees their candidate list. |
| Not an active member of the request's organization (including other tenants) | **404** |
| Active member without the Coordinator/Admin role (e.g. a volunteer of that org) | **403** `CAPABILITY_MISSING` |
| The request's senior or creator | **404**. The list holds volunteer identities and reliability scores, and BR forbids public ratings of people. |
| Coordinator or Admin of the request's organization | **200** |
| No access guard wired | **404** (fails closed) |

- **Tenant source.** The tenant is read from the request row, never from the client.
- **No caller-chosen volunteers.** The body carries only `helpRequestId`, and the server computes the candidates.
- **Staff check.** It reuses `IOrganizationAccessGuard.EnsureStaffAsync`, the same guard every coordinator endpoint uses.
- **Unguarded methods.** `FindCandidatesAsync` and `GetHybridProposalsAsync` stay on the concrete class for internal use (the tier dispatcher and tests). They are removed from `IMatchingService`, so endpoints cannot call them.

### 3.2 No client-supplied origin

**`/matching/feed`**

- `latitude`/`longitude` are no longer bound.
- The origin is the caller's own `VolunteerProfile` location.
- If the caller has none, every item gets `distanceBand: null`. The old fake 3 km default is gone.

**`/discovery/nearby-volunteers` and `/discovery/nearby-requests`**

- `latitude`/`longitude` are no longer bound.
- The origin is the caller's `SupportProfile` location, falling back to their `VolunteerProfile`.
- If there is none, the response is **409** `LOCATION_UNAVAILABLE` ("Add your address in your profile…").

**Flutter.** The app no longer sends an origin for these three calls.

### 3.3 Precision reduction — the numerical strategy

The single source for these rules is `SeniorConnect.Domain/LocationPrivacy.cs`.

1. **Grid first.** Another person's coordinate is snapped to a **0.01° grid**
   (`Math.Round(x, 2)`), about 1.1 km N–S and 0.75 km E–W in Austria, before any
   distance is computed.
   - This applies to volunteer homes (candidates, discovery) and request locations
     (feed, discovery).
   - As a result, changing the origin, the radius or the query can reveal at most
     the grid cell.
2. **Bands, not km.** Distances to people and requests are band codes only. Upper
   bounds are inclusive:

   | Code | Range |
   |---|---|
   | `KM_UNDER_1` | ≤ 1 km |
   | `KM_1_3` | > 1 – 3 km |
   | `KM_3_5` | > 3 – 5 km |
   | `KM_5_10` | > 5 – 10 km |
   | `KM_10_20` | > 10 – 20 km |
   | `KM_OVER_20` | > 20 km |

3. **Band-sharp radius filters.** `DistanceBand.IsWithin(band, radius)` snaps the
   radius up to a band edge. An item counts as within only if its whole band fits
   inside that snapped radius.
   - This covers the feed's `radiusKm`, the discovery `radiusKm` and the
     volunteer's own `MaxDistanceKm`.
   - The radius therefore cannot be binary-searched to recover the exact distance.
4. **No km in any text.** The ineligibility reason now reads "Distance exceeds the
   volunteer's maximum travel distance". The explanation and the AI reason carry
   the band code.
5. **Organizations are unchanged.** Organization branches are public offices, so
   they keep km and coordinates, and they get a band too.

**Response shapes**

| DTO | Change |
|---|---|
| `MatchingCandidate`, `VolunteerFeedItem` | `distanceKm` is replaced by `distanceBand: string?` |
| `ProximityResult` | `distanceKm`, `latitude` and `longitude` are now nullable; they are null for people and requests. A new `distanceBand` field is added. `gemeindeName` is filled from the postal code (the "approximate area"). |
| `CommunityGroupDto` | Coordinates are snapped to the grid. The stored value stays exact, so group features and proximity search are unchanged. |

### 3.4 Logging

- **No coordinate or address values in logs.** No `Log*` call passes one, and an architecture source-scan test enforces it.
- **No request-line logs.** `Microsoft.AspNetCore` stays at `Warning` in `appsettings.json` and `appsettings.Production.json`. "Request starting GET …?query" lines, which would carry query-string coordinates, are therefore never written. A test enforces this.
- **Query strings.** The people and request endpoints no longer take coordinates at all. The public-data endpoints (`nearby-organizations`, `nearest-towns`, `nearby-community-events`) still take a point, and that point is not logged.
- **What logs contain instead:** request id, user id and operation name only.

## 4. Affected endpoints (contract changes)

| Endpoint | Before | After |
|---|---|---|
| `GET /matching/requests/{id}/candidates` | Any authenticated user; `distanceKm` | Staff of the request's own org only; `distanceBand` |
| `POST /matching/proposals:hybrid` | Any authenticated user | The same rule as candidates |
| `GET /matching/feed` | `latitude`, `longitude`, `radiusKm`; `distanceKm` | `radiusKm` only; `distanceBand` (null means unknown) |
| `GET /discovery/nearby-volunteers` | `latitude`, `longitude`, `radiusKm`; fuzzed coordinates + km | `radiusKm` only; area + `distanceBand`; no coordinates, no km; 409 `LOCATION_UNAVAILABLE` |
| `GET /discovery/nearby-requests` | Same as nearby-volunteers | Same as nearby-volunteers |
| `GET /discovery/nearby-organizations` | — | Adds `distanceBand` (additive only) |
| Community group endpoints | Exact `latitude`/`longitude` | Grid-snapped |

Flutter is updated for every change above:

- The shared helper `core/format/distance_band.dart` maps band codes to text.
- New translation keys `common.distance_band.*` and `common.distance_unknown` exist in all three locales.

## 5. Tests

**Backend.** The main suite is
`tests/backend/SeniorConnect.Modules.HelpRequests.Tests/MatchingLocationHotfixTests.cs`
(18 tests).

| # | Required proof | Tests |
|---|---|---|
| 1 | Cross-tenant candidates are denied | `Candidates_CrossTenantCoordinator_Gets404` |
| 2 | An unauthorized authenticated user is denied | `Candidates_UnrelatedAuthenticatedUser_Gets404` · `Candidates_SameTenantMemberWithoutStaffRole_Gets403` · `Candidates_RequestOwner_IsNotAllowed_…` · `Candidates_CommunityRequestWithoutOrganization_…` · `Candidates_UnknownRequest_Gets404` · `Candidates_WithoutAccessGuard_FailsClosed` |
| 3 | The hybrid endpoint is protected | `HybridProposals_AreProtectedByTheSameRule` |
| 4 | Arbitrary lat/lng cannot probe | `Feed_UsesTheVolunteersStoredLocation_…` · `Feed_RadiusCannotBeBinarySearched_WithinOneBand` · `NearbyRequests_OriginIsTheCallersHome_…` · architecture test `PeopleAndRequestSearches_AcceptNoClientSuppliedOrigin` (×5 contracts) |
| 5 | Nearby volunteers expose no exact coordinates | `NearbyVolunteers_ReturnsAreaAndBand_NeverCoordinatesOrIdentity` (JSON scan) · `NearbyVolunteers_WithoutAStoredCallerLocation_…` |
| 6 | Nearby requests expose no exact coordinates | `NearbyRequests_ReturnsBandOnly_…` (JSON scan) · `NearbyRequests_WithoutAStoredCallerLocation_…` · updated `ProximityServiceTests` |
| 7 | Community groups do not leak exact coordinates | `Community.Tests/CommunityGroupLocationHotfixTests` |
| 8 | Logs contain no raw coordinates | architecture tests `NoLogStatement_WritesCoordinatesOrAddresses` · `AspNetCoreRequestLogging_WithQueryStrings_IsOff` (×2) |
| 9 | Authorized matching still works | `Candidates_SameTenantCoordinator_GetsBandedCandidates` · the hybrid coordinator case · all pre-existing matching and tier tests |
| 10 | The help-request flow still works | `HelpRequestAcceptFlow_StillWorks_AfterTheHotfix` · all pre-existing HelpRequests tests |

**Additional tests**

- **Contract shape:** `MatchingViewsOfOtherPeople_CarryBandsNotKilometres` and `ProximityResultCoordinates_AreOptional_…`.
- **Flutter:** `test/distance_band_hotfix_test.dart` parses the new shape and checks the band labels.

**Results**

- **Backend:** 465/465 pass.

  | Suite | Tests |
  |---|---|
  | TrustSafety | 48 |
  | PilotHardening | 45 |
  | Family | 50 |
  | Organizations | 54 |
  | HelpRequests | 113 |
  | Community | 90 |
  | Identity | 24 |
  | Architecture | 41 |

- **Flutter:** `flutter analyze` is clean and 691/691 tests pass.
- **Locales:** `check_locales` finds 664 keys, consistent across de/en/fa.

## 6. Residual risk

1. **Moving one's own stored home.** A user can change their own profile location
   and query again. Because of the grid and the bands, this reveals at most a
   volunteer's or request's ~1 km grid cell, which BR-GEO-06 already accepts.
   Follow-up for Phase 2: rate-limit profile-location changes.
2. **Coordinators see candidates beyond their roster.** They see banded candidates
   from every accepting volunteer. This is the existing matching behaviour, the
   callers are vetted staff, and precision is limited to the grid cell. Whether to
   restrict candidates to the roster plus consenting independents is a product
   decision for the Phase-2 ADR. **CLOSED 2026-09-25 (Phase 4,
   `docs/security/verification-and-discovery.md` §6):** candidates are the
   request organization's roster plus discoverable, verified volunteers
   (decision V-2 for product confirmation).
3. **Eligibility flip.** A coordinator could move an org request and watch a
   volunteer's `MaxDistanceKm` eligibility change. Distances use the grid cell and
   checks use bands, so this again yields at most the cell.
4. **Public-data endpoints still take a query-string point.** `nearby-organizations`,
   `nearest-towns` and `nearby-community-events` return public data only. The point
   is not logged at the configured levels, but reverse-proxy access logs outside the
   app can still see it.
5. **Exact data for all org staff.** `HelpRequestDto` still gives every staff member
   of the request's org the exact coordinates (audit L6). This is not a
   trilateration surface, but it lacks need-to-know. It belongs to the Phase-2
   disclosure policy. **CLOSED 2026-09-25 (Phase 3,
   `docs/security/location-consent-and-visibility.md` §5):** staff get at most the
   ~1 km grid cell; tests T05–T07, T11–T15.
6. **Module boundary debt.**
   - Before this change `ModuleBoundaryTests` passed vacuously:
     `Types.InCurrentDomain()` never loaded the Matching assembly.
   - It now force-loads every module. That surfaced 5 pre-existing violations:
     Matching uses internals of HelpRequests, Profiles and Notifications instead of
     their Contracts.
   - These are listed as explicit known debt. The list may only shrink: a companion
     test fails once a violation disappears, so its entry must then be removed.
   - Refactoring Matching onto Contracts ports is follow-up work, outside this
     hotfix.
7. **Older app builds.** They still send `latitude`/`longitude`, which the server
   ignores. Even so, they break against the new responses:
   - The old volunteer feed shows "0.0 km".
   - The old volunteers/requests discovery tabs show an error state, because the
     old parser expects a non-null `distanceKm`.

   Ship the new app build together with this backend. No data is exposed either way.

## 7. Hotfix status

```
CRITICAL    = PASS  (L1, L2 closed; tenant-scoped staff-only; tested)
HIGH        = PASS  (L3 closed; server-side origin; banded; tested)
MEDIUM-HIGH = PASS  (L4, L5 closed; server-side origin; no coordinates; tested)
```
