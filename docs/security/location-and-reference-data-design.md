# Location Privacy and Reference-Data Cache — Audit & Design

> Status: **DESIGN (Phase 1)**, since partly implemented:
> hotfix (`docs/security/hotfix-matching-location.md`: L1–L5, L7, logs),
> Phase 2 (`docs/architecture/reference-data-cache.md`: F-REF-1, reference cache),
> Phase 3 (`docs/security/location-consent-and-visibility.md`: consent model,
> central visibility policy, L6 closed, real geocoder replacing the fake stub).
> Phase 4 (`docs/security/verification-and-discovery.md`,
> `block-report-policy.md`, `safeguarding-location-policy.md`, ADR-026):
> verification facts (G2), discovery eligibility, blocks and safeguarding
> restrictions as policy inputs, private event venues, roster-scoped candidates.
> Coordinate encryption and the visit-address consent decision remain open.
> The text below is the original audit.
>
> Date: 2026-09-25 · Scope: backend (ASP.NET Core 10 modular monolith) and the
> Flutter app (`mobile/senior_connect`).
>
> Binding rules touched: BR-GEO-03/04/05/06, BR-COMM-04, BR-TRUST-03/05,
> BR-FAMILY-*, and the AGENTS.md "Forbidden" list. Items marked **DECISION**
> conflict with or extend `docs/product/business-rules.md`. They need an ADR
> before they are built (AGENTS.md: "do not silently change business rules").

---

## 1. Findings

### 1.1 Where coordinates live today

| Entity (module) | Columns | Meaning | Written by |
|---|---|---|---|
| `SupportProfile` (Profiles) | `latitude`, `longitude`, `postal_code`, `city`, `address_line` | Senior / citizen home | `PUT /me/support-profile`, `POST /reference/geocode-address` (`persistToProfile`) |
| `VolunteerProfile` (Profiles) | `latitude`, `longitude`, `postal_code` | Volunteer home / base | `PUT /me/volunteer-profile`, geocode endpoint |
| `HelpRequest` (HelpRequests) | `latitude`, `longitude`, `location_address`, `location_postal_code`, `location_city` | Meeting place, usually the senior's home | `POST /help-requests` (client-supplied lat/lon) |
| `CommunityGroup` (Community) | `latitude`, `longitude`, city, region, postal code | Group meeting area | Create/update group (client-supplied) |
| `OrganizationBranch` (Organizations) | `latitude`, `longitude`, city, postal code | Public office address | Org admin |
| `AustrianAdministrativeUnit` (Geography) | `latitude`, `longitude` | Gemeinde centroid (public reference data) | Seeder |

The client supplies coordinates at every write site. The server stores them
as-is. That is fine for the user's own data. It becomes dangerous when those
coordinates are used to compute answers for **other** users (§1.3).

### 1.2 Every DTO that carries coordinates

| DTO | File | Returned to | Precision today |
|---|---|---|---|
| `SupportProfileDto` | `modules/Profiles/Application/Dtos.cs:21` | Self only (`/me/support-profile`) | Exact. OK, own data. |
| `VolunteerProfileDto` | `modules/Profiles/Application/Dtos.cs:44` | Self only (`/me/volunteer-profile`) | Exact. OK, own data. |
| `GeocodeAddressResponse` | `SeniorConnect.Api/Endpoints/GeographyEndpoints.cs:244` | Caller (their own typed address) | Exact. OK. |
| `HelpRequestDto` | `modules/HelpRequests/Application/Dtos.cs:77` | Parties get exact values. Non-parties get `Coarsen()` to 2 decimals. | Exact for the senior, the creator, the assigned volunteer **and every staff member of the request's organization** |
| `ProximityResult` | `modules/Geography/Application/IGeographyReferenceService.cs:66` | Any authenticated user (`/discovery/*`) | People and requests: snapped to a 0.01° grid (`FuzzCoordinate`), distance in 0.5 km steps. Organizations: exact. |
| `NearbyTownDto` | same file `:78` | Any authenticated user | Gemeinde centroid (public). OK. |
| `GemeindeDto` | same file `:17` | Anonymous | Gemeinde centroid (public). OK. |
| `CommunityGroupDto` | `modules/Community/Application/Dtos.cs:7` | Group viewers, including non-members of public groups | **Exact**, plus `CreatorUserId` |
| `OrganizationBranchDto` | `modules/Organizations/Application/Dtos.cs:37` | Public/org viewers | Exact (public office). OK. |
| `NearbyHelpRequestRow` | `modules/HelpRequests/Contracts/DiscoveryContracts.cs:18` | Internal cross-module contract only | Exact, but never serialized to a client. OK. |

Some DTOs have **no coordinates but carry distance**. They leak just as well
(§1.3):

| DTO | Field | Precision |
|---|---|---|
| `MatchingCandidate` (`/matching/requests/{id}/candidates`) | `VolunteerUserId`, `DistanceKm` (0.1 km), `IneligibilityReasons` ("Distance (4.3 km) exceeds…"), `Breakdown.Explanation` | Computed from **exact** coordinates |
| `HybridMatchingProposal` (`/matching/proposals:hybrid`) | `CandidateVolunteerUserId`, `AiRecommendationReason` ("Optimal proximity (4.3 km)") | Exact |
| `VolunteerFeedItem` (`/matching/feed`) | `DistanceKm` (0.1 km), `LocationPostalCode`, `LocationCity` | Exact request coordinates versus a **caller-supplied** point |

### 1.3 Endpoints that can return another user's location

| # | Endpoint | Auth today | Leak | Severity |
|---|---|---|---|---|
| L1 | `GET /api/v1/matching/requests/{id}/candidates` | Any authenticated user, **any request**. There is no party, staff or tenant check. | Returns every accepting volunteer's user id plus the exact distance (0.1 km) from the request point. An attacker creates 3 help requests at points of their choosing (they control lat/lon on create) and trilaterates each volunteer's home to about 100 m. It also works across tenants. | **CRITICAL** |
| L2 | `POST /api/v1/matching/proposals:hybrid` | Same as L1 | Same data as L1, plus each volunteer's completion history | **CRITICAL** |
| L3 | `GET /api/v1/matching/feed?latitude&longitude` | Any authenticated user, not only volunteers | Distance from **exact** request coordinates to a point the caller picks, at 0.1 km precision. Moving the probe point trilaterates the senior's home. Requests outside the radius are still listed with their distance. | **HIGH** |
| L4 | `GET /api/v1/discovery/nearby-volunteers?latitude&longitude` | Any authenticated user | Returns the grid cell (~1.1 × 0.75 km) and postal code of every accepting volunteer. The caller picks any probe point, so a sweep maps every volunteer in Austria. No verification or discoverability opt-in is checked: `IsAcceptingRequests` alone makes a volunteer discoverable. | **MEDIUM-HIGH** |
| L5 | `GET /api/v1/discovery/nearby-requests?latitude&longitude` | Any authenticated user | Returns the grid cell of every open request, which is usually a senior's home. Same sweep issue as L4. | **MEDIUM** |
| L6 | `GET /api/v1/help-requests/{id}` and the open list | Authenticated | Non-parties get 2-decimal coordinates plus city and PLZ, which is acceptable. But **all staff of the org** count as parties. They see exact coordinates with no need-to-know or consent check. **CLOSED in Phase 3:** the generic DTO is capped at the ~1 km grid for everyone; exact coordinates only via `/help-requests/{id}/location` for the assigned volunteer with the senior's PRECISE consent. | **MEDIUM** |
| L7 | Community group list/detail | Authenticated | Exact lat/lon plus `CreatorUserId` for groups created by individuals. Such a group can meet in a private living room. | **MEDIUM** |
| L8 | All `GET /discovery/*` and `/matching/feed` | — | The **caller's own** home coordinates travel in the query string. Hosting diagnostics (`Request starting GET …?latitude=…` at Information level), reverse-proxy access logs and APM all see them. | **MEDIUM** (log exposure) |

Checked and found safe:

- `/me/*` profile reads return only the caller's own data.
- The geocoder stub no longer logs addresses.
- No `Log*` call writes coordinates.
- The Flutter code has no `print`/`debugPrint` of coordinates.

### 1.4 Verification model today

- **Email and phone:** `User.EmailVerifiedAtUtc` and `User.PhoneVerifiedAtUtc`,
  plus `Verification` rows (`Identity/Domain/Verification.cs`).
- **`Verification` rows** already model separate facts, which is good:
  - `Type ∈ {Email, Phone, Identity, Address, Organization, Training, BackgroundCheck}`
  - `Status`
  - `Provider ∈ {Manual, Organization, IdAustria, Kyc}`
  - `VerifiedByOrganizationId`, `ValidUntilUtc`
- **`TrustLevelCalculator` (Identity)** is a strict ladder:

  | Level | Requires |
  |---|---|
  | L1 | phone or email |
  | L2 | phone and email |
  | L3 | + address |
  | L4 | + identity |
  | L5 | + background check |

- **Gaps:**
  - **G1.** `VerificationType.Organization` is **ignored** by the trust
    calculator and by discovery. Being registered or verified by a charity
    currently changes nothing.
  - **G2.** No production code path ever creates a `Verified` **Address**
    verification, so L3+ is unreachable outside test data.
    `ManualVerificationProvider` and
    `OrganizationVerificationProvider.GetOutcomeAsync` are stubs that always
    return `IsVerified: true`. Nothing calls them today. If they were wired
    as-is, they would be self-verification holes.
  - **G3.** `modules/TrustSafety/Domain/Verification.cs` is an empty
    placeholder ("Replaced by …"). It is dead code.
  - **G4.** Discovery (`FindNearbyVolunteersAsync`) and the volunteer feed
    ignore verification entirely. Matching uses the trust level only as a hard
    gate against `RequiredTrustLevel`.

### 1.5 Organization-created / organization-verified users

- **There is no "created by organization" user concept.** The closest facts
  are:
  - An `OrganizationMembership` with `Role ∈ {Volunteer, Client}` and
    `Status = Active`.
  - An `IntakeFormSubmission` (`UserId`, `OrganizationId`, `Status`,
    `DecidedByUserId`). It is org-scoped, and staff perform its `Approve()`.
  - A `Verification` with `Provider = Organization` and
    `VerifiedByOrganizationId`. This is never created in production (G2).
- An approved intake submission creates **no** `Verification` and has no
  effect on discovery.
- "Organization-verified" therefore has to be defined. See §3.2.

### 1.6 Reference data today

**Backend**

- The endpoints `GET /api/v1/reference/austria/{bundeslaender,bezirke,gemeinden,lookup}`
  are anonymous.
- They are held in `IMemoryCache` for 24 h absolute / 4 h sliding.
- They send **no** `Cache-Control`, `ETag` or `Last-Modified` headers.
- Other slow-changing catalogues (help categories, skills, interests,
  languages) are served uncached.

**Flutter**

- `GeographyRepositoryImpl` fetches on every call. No reference data is
  cached.
- The only local persistence is `core/cache/local_read_cache.dart`: plain
  `shared_preferences` JSON under the prefix `senior_connect_cache_`.
- That cache currently stores **my schedule** (user-specific appointments). It
  has been wiped on logout since update #5.
- Tokens live in `flutter_secure_storage`.
- **Drift is not installed.** `system-design.md` schedules Drift for Phase 7
  (P7-04, the offline store). It is not selected for reference data.

**Contract bug (F-REF-1)**

- The backend `GemeindeDto(Code, Name, PostalCode, Latitude, Longitude)`
  serializes as `code`/`name`.
- Flutter `Gemeinde.fromJson` reads `json['gemeindeCode']` and
  `json['gemeindeName']` with `as String`. That throws a `TypeError`.
- So `getGemeinden()` and `lookupByPlz()` most likely fail at runtime today.
- Verify with a contract test before fixing (§8.2).
- **Fixed 2026-09-25 (Phase 2):** the Flutter model now reads `code`/`name`;
  see `docs/architecture/reference-data-cache.md` §8.

---

## 2. Security / privacy risks (ranked)

1. **Trilateration of volunteer homes via matching (L1/L2). CRITICAL.** Any
   logged-in account can locate every volunteer to about 100 m and link the
   location to a user id. This is a stalking risk for a vulnerable population.
2. **Trilateration of senior homes via the volunteer feed (L3). HIGH.** It
   reveals the homes of people who live alone and asked for help.
3. **Nation-wide sweep of fuzzed cells (L4/L5). MEDIUM-HIGH.** Grid fuzzing is
   only safe when the probe point is not attacker-controlled and the opt-in
   cannot be bypassed.
4. **Over-broad staff access (L6). MEDIUM.** Every org staff member sees the
   exact coordinates of every request in the org, with no need-to-know and no
   audit.
5. **Exact group coordinates (L7). MEDIUM.**
6. **Caller location in URLs and logs (L8). MEDIUM.**
7. **Verification stubs that return "verified" (G2). MEDIUM.** Wired as they
   are, they would bypass authorization.
8. **No consent record for location sharing. MEDIUM (compliance).** GDPR
   Art. 7 requires the controller to be able to demonstrate consent, which is
   impossible today.
9. **Reference-data cache (F-REF-1, plus no caching). LOW.** A functional and
   cost issue, not a privacy one.

---

## 3. Proposed architecture

### 3.1 Location model

| Concept | Definition | Where |
|---|---|---|
| **Stored coordinate** | The exact WGS-84 point the user or organization entered or geocoded. Used **only server-side** for matching and radius math. Never serialized to anyone except its owner. Existing columns are kept. | `support_profiles`, `volunteer_profiles`, `help_requests` (unchanged) |
| **Discoverability** | Whether the user may appear at all in *other users'* discovery or matching results. This is a separate boolean from availability: `IsAcceptingRequests` means "I take requests", `IsDiscoverable` means "others may see that I exist nearby". New volunteers default to `false`. Existing volunteers are backfilled from their `IsAcceptingRequests` value so behavior does not change (§6). | new `user_location_privacy.is_discoverable` |
| **Approximate location** | What others may see without precise consent: the **Gemeinde name** (and Bezirk) plus a **distance band**. It contains **no coordinates** for people or requests (see "Why no coordinates at all" below). The server computes it from the stored coordinate. | Response projection only |
| **Distance band** | `LT_1`, `1_2`, `2_5`, `5_10`, `10_20`, `GT_20` km. Computed from the **fuzzed cell centre to the caller's own stored location**, never to a probe point the caller supplies. | Response projection |
| **Precise location** | The exact coordinate and/or street address, disclosed to a *specific* counterpart. | Response projection |
| **Explicit consent** | A `Consent` row (existing entity) with the new `ConsentType.PreciseLocationSharing`, `Granted = true`, `DocumentVersion`, `GrantedAtUtc` and `IpHash`. Given in-app by an affirmative action, never pre-ticked. | `consents` |
| **Consent timestamp** | `Consent.GrantedAtUtc` (already exists). | `consents` |
| **Consent version** | `Consent.DocumentVersion`, e.g. `location-v1`. The server holds the *current* version. A stored version older than current is treated as **absent** until the user re-consents. | `consents` + config `Privacy:LocationConsentVersion` |
| **Revocation** | `Consent.Withdraw()` sets `WithdrawnAtUtc` and `Granted = false`. Takes effect **on the next request**, because nothing precise about others is cached client-side (§3.5). It cannot recall what was already shown; the UI says so. | `consents` |
| **Verification state** | Separate facts (§3.2). They gate *eligibility to be discovered or matched*. They **never** grant precise disclosure. | computed |

**Precise disclosure rule.** Enforced server-side by a single policy class,
`LocationDisclosurePolicy`. Exact data about subject S is returned to viewer V
**only if all of the following hold**:

1. **Relationship.** V has a qualifying relationship to S for this specific
   record. It must be one of:
   - V is the confirmed assignee of one of S's help requests (status
     `Assigned` or later, not cancelled).
   - V is the senior or creator of a request assigned to S.
   - V is a linked family caregiver with `ViewLocation` permission.
   - V is a coordinator of the *same tenant* acting on S's case
     (§3.3 matrix).
2. **Consent.** One of these must apply:
   - S holds a current, non-withdrawn `PreciseLocationSharing` consent.
   - The record's own consent flag applies. For a help request this is
     `share_exact_location_with_assignee = true`, captured at creation.
3. **Safeguarding exception.** Safeguarding-authorized staff of the same tenant
   who have an **open safeguarding case** on S may see the exact location
   **without consent** (vital-interest basis, GDPR Art. 6(1)(d)). Every such
   disclosure is audited the way break-glass access is. **DECISION / Legal
   Gate.**
4. **Otherwise** V gets the approximate projection. If V is not entitled to
   that either, V gets nothing.

> **DECISION D1 (conflicts with BR-GEO-06 / BR-COMM-04).** Today the exact
> address is revealed automatically after assignment. This design adds an
> explicit per-request consent flag. It defaults to **unticked** and comes with
> a clear explanation on the create screen. Without the flag, the assigned
> volunteer receives the address text only when the senior confirms the
> assignment. This needs an ADR and a rewording of BR-GEO-06.

**Why no coordinates at all in approximate views.**

- A rounded coordinate repeated across many results still allows clustering
  and correlation.
- The Flutter app does not draw pins for other people today; the discovery
  screens are lists.
- Dropping the field is simpler than fuzzing it better, and strictly safer.
- Exceptions: organizations and branches keep exact coordinates (public office
  data), and Gemeinde reference data keeps its centroids.

### 3.2 Verification model

Keep the existing separate facts and compute them with one reader. Never store
a combined boolean.

```
emailVerified         = User.EmailVerifiedAtUtc != null  || Verification(Email, Verified, !expired)
phoneVerified         = User.PhoneVerifiedAtUtc != null  || Verification(Phone, Verified, !expired)
addressVerified       = Verification(Address, Verified, !expired)
                        — created ONLY by a coordinator action (manual check) or a
                          future postal-code letter flow; never self-service
organizationVerified  = exists Verification(Organization, Verified, !expired,
                                            VerifiedByOrganizationId = O)
                        AND active OrganizationMembership(user, O, Role ∈ {Volunteer, Client})
                        — created when staff approve an IntakeFormSubmission
                          (Approve() emits it, same transaction)
```

- **Read model:** `VerificationFacts`, new, in `Identity.Contracts`.
- **Consumers:** Matching and Geography read it through
  `IVerificationFactsReader`.

**Discovery eligibility level.** This is new and separate from the trust
ladder. The trust ladder stays as it is and keeps gating `RequiredTrustLevel`.

| Level | Condition | Effect |
|---|---|---|
| `D0 NotEligible` | None of the conditions below holds | Never appears in anyone's discovery or matching |
| `D1 Community` | `emailVerified && phoneVerified && addressVerified` | May appear in community-wide (non-org) discovery and matching if `IsDiscoverable` |
| `D2 Organization(O)` | `organizationVerified` for org O | May appear in O's coordinator matching (tenant-scoped) regardless of `IsDiscoverable`. Community-wide appearance still needs `IsDiscoverable` **and** the org policy `AllowCommunityDiscovery` (new `OrganizationPolicy` flag, default false) |

A user can be D1 and D2 at the same time. Precise disclosure is **not** a
function of D. It is decided only by §3.1.

> **DECISION D2.** `TrustLevelCalculator` ignores `Organization`
> verification. Should org verification count toward trust, for example as
> equivalent to "address verified" for L3? This design leaves the calculator
> unchanged. Flag it for the Trust ADR.

### 3.3 Location visibility matrix

- "Distance" is always a band, measured from the viewer's **stored** location.
- "Area" is the Gemeinde name.
- "Exact" means coordinates or street address.

**Subject: a senior, citizen or volunteer**

| Viewer | Discover? | Approx. area? | Distance? | Exact? | Conditions |
|---|---|---|---|---|---|
| Anonymous | No | No | No | No | Reference data and public org branches only |
| Authenticated user (no role, L0) | No | No | No | No | Must reach L1 (phone verified) to use discovery at all. Rate-limited. |
| Senior / citizen (L1+) | Volunteers: yes, if the subject is D1 and `IsDiscoverable` | Yes | Yes | Only their confirmed assignee, and only if the assignee holds `PreciseLocationSharing` | Viewer must have a stored location. No probe points. |
| Volunteer (not verified, D0) | Open requests: yes | Yes (request area) | Yes | No | Sees requests, never other volunteers |
| Verified volunteer (D1) | Open requests: yes | Yes | Yes | Address of a request they are the confirmed assignee of, if the request has `share_exact_location_with_assignee` or the senior confirmed (D1) | Becomes a party only after assignment |
| Organization-created or verified user (D2), as **subject** | Inside org O: yes, for O's coordinators. Community-wide: only with `IsDiscoverable` and the org policy | Yes | Yes | Same rules as the rows above | Org policy may restrict further, never widen |
| Organization coordinator (tenant O) | Members and clients of O, requests of O | Yes | Yes | Only for a request or case of O they are acting on, **and** only with subject consent or the request consent flag. Every exact read is audited. | Cross-tenant: 404. Never outside O. |
| Organization admin (tenant O) | Aggregates only | Aggregates only | No | **No** | Admins manage the org, not cases. Case work needs the coordinator role. |
| Family caregiver | Only the linked senior | Yes (linked senior) | n/a | Linked senior only, with `FamilyPermission.ViewLocation` **and** the senior's `PreciseLocationSharing` consent. Never volunteers. | BR-FAMILY-01/03 permission model; defaults off |
| Safeguarding-authorized staff (tenant O) | Subjects of their open cases | Yes | Yes | Yes, **without consent**, for a subject with an open case in O. Every read is audited. **DECISION / Legal Gate.** | Cross-tenant: 404 |
| The subject themself | — | — | — | Own exact data, always | `/me/*` |

**Other subjects**

- **Help request (the request is the subject).** Non-parties see the area and
  the distance band. The assignee sees exact data per §3.1. The senior and the
  creator always see exact data.
- **Community group created by an individual.** Members see the area and the
  distance. Only the creator and the group admins see exact coordinates.
- **Community group created by an org:** exact coordinates are public.
- **Organization branch:** exact coordinates are public.

### 3.4 Server enforcement design

- **One policy.** `LocationDisclosurePolicy` lives in `SeniorConnect.Domain`
  (shared, pure): `Decide(viewerContext, subjectFacts, relationship) →
  {None | Approximate | Precise}`. Every mapper that emits a location calls it.
  No DTO mapper decides location on its own.
- **One projection.** A shared `LocationView` record replaces raw
  `Latitude`/`Longitude` in every cross-user DTO:
  `(string? AreaName, string? PostalCode, DistanceBand? Distance, PreciseLocation? Precise)`.
  `PreciseLocation(double Lat, double Lon, string? AddressLine)` is populated
  only when the policy returns `Precise`.
- **No probe points.**
  - Discovery and the feed measure distance from the caller's stored
    location, read server-side.
  - The `latitude`/`longitude` query parameters are **removed** from the
    people and request endpoints (§5). During a one-release deprecation period
    they are accepted but ignored.
  - Public-data endpoints (orgs, towns, events) still accept a point. It is
    rounded to 2 decimals **before** use and stripped from logs by the
    request-logging enricher.
- **Candidates and proposals** are staff-only. `EnsureStaffAsync` runs on the
  request's org; any other caller gets 404. Responses carry distance bands,
  and reasons are codes (`DISTANCE_EXCEEDS_MAX`), not numbers.
- **Tenant isolation.**
  - Every org-scoped read goes through `IOrganizationAccessGuard`.
  - A request without an org never appears in any coordinator's candidate
    view.
  - The org query filters stay unchanged.
- **Audit.** Each `Precise` decision for a viewer who is not the subject writes
  a `location.precise_disclosed` audit event (viewer, subject, record, reason,
  consent id). The event **does not contain the coordinates**.
- **Logs.**
  - A Serilog/`ILogger` destructuring policy drops properties named
    `*latitude*`, `*longitude*` and `*address*`.
  - HTTP request logging must not log query strings for `/discovery` and
    `/matching`.
  - A unit test asserts that the log output of a discovery call contains none
    of the digits of the caller's coordinates.

### 3.5 Reference-data cache model

**Qualifies as reference data** only if it is all of these:

- public;
- identical for every user in a locale;
- not derived from any user's data;
- changed at most weekly.

| Dataset | Endpoint | TTL (client) | Notes |
|---|---|---|---|
| Bundesländer | `GET /reference/austria/bundeslaender` | 30 days | 9 rows |
| Bezirke per Bundesland | `GET /reference/austria/bezirke?bundeslandCode=` | 30 days | |
| Gemeinden per Bezirk | `GET /reference/austria/gemeinden?bezirkCode=` | 30 days | Centroid coordinates are public |
| PLZ lookup | `GET /reference/austria/lookup?plz=` | 30 days | One key per PLZ |
| Skills, interests, languages catalogues | existing catalogue endpoints | 7 days | Localized; the cache key includes the locale |
| Help categories | existing endpoint | **1 day**, display only | Carries `IsBlocked` and `SafetyLevel`. The client never enforces these from cache; the server re-checks them at create time (BR-SAFETY). |

**Never cached as reference data.** None of these may go into the reference
cache:

- help requests
- safeguarding records
- messages
- emergency data
- family links
- private org records
- any profile or location
- discovery and matching results (they are relative to the user)
- tokens (they stay in secure storage)
- trust and verification state
- anything under `/me/*`

**Versioning and ETag**

- **Server.** Each reference response gets:
  - `Cache-Control: public, max-age=<TTL>, stale-while-revalidate=604800`
  - `ETag: "<datasetVersion>-<first 16 hex of sha256(payload)>"`
  - `304` with no body when `If-None-Match` matches.

  The hash is computed once, when the in-memory cache entry is filled. **No
  table and no migration are needed.** A seeder update changes the payload,
  and with it the ETag.
- **Client envelope:** `{schema: 1, etag, fetchedAtUtc, locale, payload}`.
- **Client key:** `ref:<dataset>:<param>:<locale>:v<schema>`.
- **Schema bumps.** Bumping `schema` in the app (for example when a DTO shape
  changes, as it must for F-REF-1) orphans the old keys. Orphaned keys are
  purged at startup.

**Read path (stale-while-revalidate)**

1. Cache hit younger than the TTL: return it. No network call.
2. Cache hit older than the TTL:
   - Return the cached value immediately.
   - Revalidate with `If-None-Match`.
   - On `304`, touch `fetchedAtUtc`.
   - On `200`, replace the entry and notify listeners.
3. Cache miss:
   - Fetch from the network.
   - On success, store the result.
   - On network failure, show the error state with retry.
4. Offline or 5xx, with a cached entry:
   - Serve the entry, whatever its age.
   - Mark it `isStale`, so the UI can show a subtle "Stand: <date>" where
     relevant.
   - A cold cache never blocks address entry. The user can still type the
     PLZ, and the server validates it on save.

**Logout**

- Reference data is **not** user data, so it **survives** logout.
- `LocalReadCache.clearAll()` (the session wipe) must delete only the *user*
  namespace.
- The reference cache uses a separate prefix (`senior_connect_ref_`) and its
  own `purgeAll()`.

**Clearing rules.** The reference cache is purged when:

- the schema version changes;
- the app locale changes (only the old locale's keys);
- the user runs "Clear local data" in Settings;
- the account is deleted.

**Storage choice**

- Use the existing `LocalReadCache` / `shared_preferences` mechanism under its
  own prefix. No new package is needed.
- **Drift** remains the Phase-7 choice for the operational offline store
  (P7-04). It is not selected for reference data.
- The largest payload is small. All Gemeinden of one Bezirk, or all 2,093 in a
  later version, come to about 150 KB. That is well within `shared_preferences`
  limits.
- If the full national list is ever preloaded as one piece, move just that
  dataset to Drift when P7-04 lands.
- **DECISION D3:** confirm that pulling Drift forward is *not* wanted.

**Existing user cache (my schedule)**

- It is user-specific operational data, stored in plaintext
  `shared_preferences`.
- It is acceptable for P7-04: it is wiped on logout and contains no location.
- Keep it out of the reference namespace.
- Flag for P7-04: move it to Drift with SQLCipher, or to secure storage.

---

## 4. Files likely to change

### Backend

| File | Change |
|---|---|
| `SeniorConnect.Domain/LocationDisclosurePolicy.cs` (new) | Pure policy, plus `DistanceBand`, `LocationView`, `PreciseLocation` |
| `modules/Identity/Domain/Consent.cs` | Add `ConsentType.PreciseLocationSharing` |
| `modules/Identity/Infrastructure/IdentityConfigurations.cs` | Add the new value to `ck_consents_type` |
| `modules/Identity/Contracts/*` (new `IVerificationFactsReader`, `VerificationFacts`) | Facts reader |
| `modules/Identity/Infrastructure/VerificationProviders.cs` | Remove the always-true stubs, or restrict them to Development |
| `modules/TrustSafety/Domain/Verification.cs` | Delete the placeholder |
| `modules/Organizations/Domain/IntakeForms.cs` + `OrganizationService.cs` | `Approve()` emits an Organization verification (through a contract) |
| `modules/Organizations/Domain/OrganizationPolicy.cs` | Add `AllowCommunityDiscovery` |
| `modules/Profiles/Domain/UserLocationPrivacy.cs` (new), with its configuration, service and DTOs | `IsDiscoverable` and the consent link |
| `modules/Geography/Application/IGeographyReferenceService.cs` | `ProximityResult` becomes `LocationView`-based. People lose `UserId` and coordinates. |
| `modules/Geography/Infrastructure/GeographyServices.cs` | Distance from the caller's stored location, eligibility filter, bands, ETag hash |
| `modules/Matching/Infrastructure/MatchingService.cs` + `Application/IMatchingService.cs` + `Domain` records | Staff guard, bands, reason codes, feed from the stored location, radius filter |
| `SeniorConnect.Api/Endpoints/MatchingEndpoints.cs` | Pass the caller id, drop the lat/lon parameters |
| `SeniorConnect.Api/Endpoints/GeographyEndpoints.cs` | Drop probe parameters on people and request routes. Add ETag/304. Require L1. |
| `modules/HelpRequests/Infrastructure/HelpRequestService.cs` + `Domain/HelpRequest.cs` + DTOs | Policy-based `MapRequest`, consent flag on create, staff need-to-know |
| `modules/Community/Infrastructure/CommunityService.cs` + DTOs | Policy-based group location |
| `SeniorConnect.Api/Program.cs` | Request-logging redaction, log destructuring policy |
| `SeniorConnect.Infrastructure/Migrations/*` | See §6 |
| `docs/product/business-rules.md`, `docs/architecture/authorization.md`, new ADR | Decisions D1–D3 |

### Flutter

| File | Change |
|---|---|
| `lib/core/cache/reference_data_cache.dart` (new) | Envelope, TTL, ETag, stale-while-revalidate, own prefix |
| `lib/core/cache/local_read_cache.dart` | `clearAll()` must keep the reference prefix. A separate prefix already makes this automatic. |
| `lib/core/network/api_client.dart` | New `getWithEtag()` helper that returns a 304-aware response |
| `lib/features/profile/data/geography_repository.dart` | Fix the `Gemeinde.fromJson` keys (F-REF-1), use the cache |
| `lib/features/discovery/data/discovery_repository.dart` | `LocationView` model; no probe coordinates sent |
| `lib/features/discovery/application/discovery_notifier.dart` | Stop reading and sending own coordinates; handle "no stored location" |
| `lib/features/community/application/community_feed_notifier.dart` | Same as above |
| `lib/features/community/domain/community_group.dart` | Nullable `precise`, area plus band |
| Matching/feed data and models (volunteer feed, candidates) | Distance band enum, reason codes |
| `lib/features/help_requests/...` create flow | Consent checkbox "Genaue Adresse nach Zusage teilen" |
| `lib/features/help_requests/...` detail flow | Show the precise block only when it is present |
| `lib/features/profile/presentation/location_privacy_screen.dart` (new), with notifier and repository | Discoverable toggle, precise-sharing consent, revoke |
| `assets/translations/{de,en,fa}.json` | Band labels, consent texts, privacy screen, stale-data hint |

---

## 5. API contract changes (⚠ = breaking)

| Endpoint | Change |
|---|---|
| ⚠ `GET /matching/requests/{id}/candidates` | Staff of the request's org only; everyone else gets 404. `distanceKm` becomes `distanceBand`. `ineligibilityReasons` becomes an array of codes. The explanation contains no numbers. |
| ⚠ `POST /matching/proposals:hybrid` | Same guard as candidates. `aiRecommendationReason` becomes a code plus a band. |
| ⚠ `GET /matching/feed` | `latitude`/`longitude` removed (ignored for one release). Volunteers only; anyone else gets 403 `VOLUNTEER_PROFILE_REQUIRED`. Returns only items within the radius. `distanceKm` becomes `distanceBand`. Returns 409 `LOCATION_REQUIRED` when the caller has no stored location. |
| ⚠ `GET /discovery/nearby-volunteers` | Probe parameters removed. Caller must be L1+. Each item is `{areaName, postalCode, distanceBand}`: no `userId`, `latitude`, `longitude` or `isFuzzed`. Only D1 subjects with `isDiscoverable` are returned. |
| ⚠ `GET /discovery/nearby-requests` | Same changes as nearby-volunteers. Items gain `helpRequestId` so the request can be opened; the detail DTO is policy-guarded itself. |
| `GET /discovery/nearby-organizations`, `/nearest-towns`, `/nearby-community-events` | Public data. They still accept a point, rounded to 2 decimals server-side. May move to the stored location later. |
| ⚠ `GET /help-requests/{id}` and the lists | `latitude`, `longitude` and `locationAddress` are replaced by `location: LocationView`. |
| ⚠ `POST /help-requests` | New `shareExactLocationWithAssignee: bool`. Required, with no default in the API. |
| ⚠ Community group DTOs | For groups created by individuals, `latitude`/`longitude` are replaced by `location: LocationView`. |
| New: `GET/PUT /api/v1/me/location-privacy` | Body `{isDiscoverable, preciseSharing: {granted, version, grantedAtUtc}}`. PUT toggles `isDiscoverable`. |
| New: `POST` and `DELETE /api/v1/me/consents/precise-location` | Grant (records the version and IP hash) or revoke. Honours `Idempotency-Key`. |
| New: `GET /api/v1/me/verification-facts` (or extend `/me/trust`) | Returns `{emailVerified, phoneVerified, addressVerified, organizationVerified: [orgId…], discoveryLevel}`. Read-only; the client never sends these. |
| Reference endpoints | Add `ETag`, `Cache-Control` and `304`. Fix the `GemeindeDto` naming mismatch: keep the backend's `code`/`name` and fix Flutter (recommended). |

---

## 6. Database migration plan

A single EF migration, `LocationPrivacyAndConsent`, created with
`dotnet ef migrations add`. No manual DDL.

1. **`consents` table.** Recreate `ck_consents_type` with
   `'PreciseLocationSharing'` added. Additive; no data changes.
2. **New `user_location_privacy` table** (Profiles module):
   - `user_id uuid` primary key, referencing `users.id` (the only
     cross-module reference allowed)
   - `is_discoverable boolean not null default false`
   - `created_at_utc`, `created_by`, `updated_at_utc`, `updated_by`, `xmin`
3. **Backfill `user_location_privacy`.**
   - Insert one row per existing volunteer profile, with
     `is_discoverable = is_accepting_requests`.
   - All other users get no row, which means not discoverable.
   - This preserves today's behavior.
   - **DECISION D4:** should existing volunteers instead get a one-time in-app
     prompt and start at `false`? That is more privacy-protective.
4. **`help_requests` table.**
   - Add `share_exact_location_with_assignee boolean not null default false`.
   - Add `exact_location_consent_at_utc timestamptz null`.
   - Backfill existing rows with `true` **only** where
     `assigned_volunteer_user_id IS NOT NULL`, so in-flight assignments keep
     working. All other rows get `false`.
5. **`organization_policies` table.** Add
   `allow_community_discovery boolean not null default false`.
6. **Coordinate columns stay unchanged.** No column encryption in this step.
   **DECISION D5:** consider pgcrypto or app-level encryption for
   `support_profiles.latitude`, `longitude` and `address_line` as a separate
   hardening item.
7. **Audit.** The existing audit table receives `location.precise_disclosed`
   events. If the audit event-type column has a check constraint, extend it in
   the same migration.

The migration is reversible. `Down` drops the new table and columns and
restores the old constraint.

---

## 7. Flutter implementation plan

1. **F-REF-1 fix plus contract test first.**
   - Change `Gemeinde.fromJson` to read `code`/`name`.
   - Add a test that uses a real backend-shaped fixture.
2. **`ReferenceDataCache`** (in `core/cache/`).
   - API: `Future<CachedValue<T>> get(dataset, params, fetcher, ttl)`,
     returning `{value, isStale}`.
   - A `Stream` refreshes consumers after revalidation.
   - Every read and write sits in try/catch and falls back to the network.
3. **Repositories and notifiers.** `GeographyRepositoryImpl` and the catalogue
   repositories use the cache. Notifiers keep the sealed AsyncState
   (ADR-022).
   - A stale value renders as a normal loaded state.
   - A failure with a cold cache shows the error state with retry.
4. **Discovery, feed and community.**
   - Remove the `getMyLocation()` plus query-parameter pattern.
   - The repository calls the endpoints without coordinates.
   - Handle `LOCATION_REQUIRED` with an empty state whose action is "Adresse
     im Profil hinterlegen".
5. **Models.**
   - `LocationView` has `areaName`, `postalCode`, `distanceBand` and an
     optional `precise`.
   - A `DistanceBand` enum maps the server codes. Translation happens only in
     presentation.
6. **Help request create.**
   - Add an explicit consent question, unticked by default, with a
     plain-language explanation.
   - An answer is required (radio "Ja / Nein"), so it is never silently false.
   - In Senior Mode: one question per screen, 64 dp touch targets.
7. **Help request detail.**
   - Show the street and map only when `precise` is non-null.
   - Otherwise show the area and the band, plus a hint explaining when the
     address becomes visible.
8. **Location privacy screen** (Profile → „Standort & Sichtbarkeit").
   - A discoverable switch.
   - The precise-sharing consent, with version text, grant date and a revoke
     action.
   - A revoke confirmation dialog that explains what cannot be recalled.
   - Reachable in Senior Mode.
9. **Localization.**
   - Add the keys in all locales, `de` first.
   - Run `check_locales`.
   - Verify RTL (fa), dark mode, and text scales 1.0, 1.5 and 2.0.
10. **Flutter never decides visibility.**
    - It renders what the server returned.
    - There are no client-side "isVerified" or "canSeeExact" flags.

---

## 8. Test plan

### 8.1 Backend unit
- **`LocationDisclosurePolicy`.**
  - A table-driven test over the full §3.3 matrix: viewer × relationship ×
    consent state.
  - Include consent that is withdrawn, outdated in version, or missing.
  - Include the safeguarding exception.
- **`DistanceBand`.** Test the boundaries: 0.99, 1.0, 1.99, 2.0 … 20.0, 20.01.
- **`VerificationFacts`.**
  - Each fact on its own.
  - Expired verifications.
  - An org verification without an active membership must evaluate to false.
  - The D0, D1 and D2 levels.
  - Only a staff actor can create an address verification.
- **`IntakeFormSubmission.Approve()`.** It emits an Organization verification
  for exactly that org.
- **Consent.**
  - Grant, withdraw and re-grant each create a new row.
  - A consent with an old version is treated as absent.

### 8.2 Backend integration (API level; each covers 401 / 403 / 404 / 200)
- **Candidates and proposals.**
  - An unauthenticated caller gets 401.
  - A non-staff caller gets 404.
  - Staff of another tenant get 404.
  - Own-org staff get 200 with bands only.
  - The response contains no numeric distance.
- **Feed.**
  - A non-volunteer gets 403.
  - A caller with no stored location gets 409.
  - Query lat/lon parameters are ignored: two calls with different probe
    points return identical results.
- **nearby-volunteers.**
  - A D0 volunteer, or one with `IsDiscoverable = false`, never appears.
  - A D1 volunteer appears as area plus band, with no userId and no
    coordinates.
  - An L0 caller gets 403.
- **Help request detail.**
  - An assignee **without** the consent flag gets no `precise`.
  - An assignee **with** the flag gets `precise`.
  - A coordinator from another tenant gets 404.
  - An org admin who is not a coordinator gets no `precise`.
  - A safeguarding officer with an open case gets `precise`, and an audit row
    is written.
- **Community group created by an individual.** A non-member gets no
  coordinates.
- **Reference data.**
  - The first call returns 200 with an `ETag`.
  - The second call, sent with `If-None-Match`, returns 304 with an empty
    body.
  - After a data change, the ETag changes.
  - A contract snapshot test pins the `GemeindeDto` field names.
- **Log redaction.**
  - Capture the logs during a discovery or feed call.
  - Assert that they contain no `latitude=` string and no coordinate digits.

### 8.3 Privacy and attack tests (named regressions)
- **`Trilateration_via_candidates_is_impossible`.**
  1. As a senior, create 3 requests at known points.
  2. Call candidates for each one.
  3. Expect 404, or bands only, and no volunteer id for non-staff.
- **`Probe_point_sweep_returns_same_result`.** Discovery results do not change
  with a caller-supplied point.
- **`Revocation_takes_effect_next_request`.**
  1. Grant consent.
  2. Read: `precise` is present.
  3. Revoke.
  4. The next read has no `precise`.
- **`No_cross_user_dto_contains_raw_coordinates`.**
  - A reflection or architecture test.
  - It fails if any DTO in a cross-user response namespace declares
    `Latitude`/`Longitude` properties outside `PreciseLocation` or the
    allow-list.
  - Allow-list: `OrganizationBranchDto`, `GemeindeDto`, `NearbyTownDto`, and
    the caller's own `/me` DTOs.

### 8.4 Flutter
- **`ReferenceDataCache` unit tests.**
  - A fresh hit makes no network call.
  - A stale hit returns the cached value, then revalidates.
  - A 304 keeps the payload.
  - Offline with a cached entry returns it with `isStale`.
  - Offline with a cold cache returns an error.
  - A schema bump purges old entries.
  - `LocalReadCache.clearAll()` keeps the reference entries.
- **Repository contract tests.** Use backend-shaped fixtures; these would have
  caught F-REF-1.
- **Widget tests.**
  - Privacy screen: grant, revoke with the confirmation dialog, and all five
    states.
  - Help-request create: the consent question must be answered.
  - Help-request detail: the precise block is hidden when absent.
  - Discovery: the `LOCATION_REQUIRED` empty state with its action.
  - Golden or semantic checks for the fa (RTL) locale and text scale 2.0.

### 8.5 E2E
- **J-LOC-1.**
  1. A senior creates a request without precise consent.
  2. A volunteer accepts it.
  3. The senior confirms.
  4. Before confirmation the volunteer sees area and band; after it, the
     address (per D1).
- **J-LOC-2.**
  1. A volunteer turns discoverable off.
  2. The senior's discovery no longer lists them.
- **J-LOC-3.**
  1. An org coordinator approves an intake.
  2. The user becomes D2.
  3. The user appears in that org's matching only.
- **J-REF-1.**
  1. Open the address form online.
  2. Kill the network.
  3. Reopen the form. The dropdowns are served from cache.
  4. Log out and back in. The reference cache is still there; the schedule
     cache is gone.

---

## 9. Acceptance criteria

1. **No cross-user exact location.** No API response returns another user's
   exact coordinates or street address unless `LocationDisclosurePolicy`
   returned `Precise` for that viewer and that record. The architecture test
   in §8.3 enforces this.
2. **Distances are banded.** No endpoint returns a numeric distance to another
   user. Bands are always measured from the caller's stored location. No
   people or request endpoint accepts caller-supplied probe coordinates.
3. **Matching is staff-only and tenant-guarded.** Non-staff and cross-tenant
   callers get 404 from candidates and proposals.
4. **Discovery requires verification and opt-in.**
   - A volunteer appears in community discovery only when the server-computed
     facts give D1 **and** `isDiscoverable = true`.
   - Org-verified users appear to their own org's coordinators, per D2.
5. **Precise-location consent is recorded and revocable.** It is stored with
   version, timestamp and IP hash. Revocation stops disclosure on the next
   request. An outdated version counts as no consent.
6. **Verification is server-side only.**
   - Verification facts are computed only by the server.
   - No client-sent verification or permission flag is read anywhere.
   - No always-true verification provider exists outside Development.
7. **Precise disclosures are audited.** Every precise disclosure to someone
   other than the subject writes an audit event, which contains no
   coordinates.
8. **Logs contain no location.** No log line contains coordinates or
   addresses, and a test verifies this.
9. **Reference data is cacheable and handled correctly on the client.**
   - Reference endpoints return `ETag` and `Cache-Control` and answer `304`
     correctly.
   - In Flutter, reference data survives logout and works offline once
     cached.
   - The user cache is wiped on logout.
   - Help-category safety flags are never enforced from cache.
10. **F-REF-1 is fixed** and covered by a contract test.
11. **Decisions and documentation are complete.**
    - Decisions D1–D5 are recorded in an ADR before the implementation phase
      starts.
    - `business-rules.md` BR-GEO-06 and BR-COMM-04 are reworded accordingly.
    - All three locales, RTL, dark mode and text scale 2.0 are verified for
      the new screens.

---

## 10. Open decisions (each needs an owner before Phase 2)

| ID | Question | Recommendation |
|---|---|---|
| D1 | Should disclosing the exact address to the assignee require an explicit per-request consent, instead of the automatic reveal after assignment? | Yes. Needs an ADR plus a rewrite of BR-GEO-06 and BR-COMM-04. |
| D2 | Should organization verification count toward the trust ladder? | Keep it separate for now; decide in the Trust ADR. |
| D3 | Should Drift be pulled forward for reference data? | No. Use `shared_preferences` with a separate prefix, and move to Drift with P7-04. |
| D4 | How should `discoverable` be initialized for existing volunteers? | A one-time prompt starting at `false` (privacy-protective) rather than a backfill from `IsAcceptingRequests`. This is a product decision. |
| D5 | Should the stored home coordinates and address be encrypted at rest? | Yes, as a separate hardening item, once key management is decided. |
| D6 | Should safeguarding staff get exact access without consent? | Legal Gate. Confirm the Art. 6(1)(d) basis and the audit duty with the DPO. |
