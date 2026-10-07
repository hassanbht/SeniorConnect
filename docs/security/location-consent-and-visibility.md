# Location consent and visibility (Phase 3)

> Status: implemented 2026-09-25. Nothing is committed.
>
> Related documents:
> - `docs/security/location-and-reference-data-design.md` (Phase 1 audit and design)
> - `docs/security/hotfix-matching-location.md` (the security hotfix)
> - `docs/architecture/reference-data-cache.md` (Phase 2)
>
> Out of scope for this phase:
> - the trust and verification redesign;
> - ID Austria;
> - the safeguarding exception;
> - encrypting coordinates in the database;
> - Drift and offline support.

## 1. Location concepts are kept separate

| | Concept | Where it lives | Phase 3 |
|---|---|---|---|
| A | The person has a stored location | `SupportProfile` / `VolunteerProfile` latitude and longitude (unchanged schema) | reused |
| B | The location is accurate | `GeoCoordinate` validation; the geocoder resolves addresses against the Austrian seed | done |
| C | The person is discoverable | `VolunteerProfile.IsAcceptingRequests` (unchanged) | unchanged |
| D | An approximate location may be shown | consent `APPROXIMATE`, or a relationship that allows approximate | done |
| E | A precise location may be shown | consent `PRECISE` **and** an authorized relationship | done |
| F | The person is verified | trust level / verification | **not an input** to any location decision |
| G | The person belongs to an organization | `OrganizationMembership` | never unlocks precise coordinates |
| H | There is an assignment relationship | `HelpRequest.AssignedVolunteerUserId` (the existing domain relationship) | the standard path to precise |

## 2. Consent model

Each person's consent is stored in `location_sharing_consents`, entity `Profiles.Domain.LocationSharingConsent`.

| Column | Meaning |
|---|---|
| `user_id` | whose consent this is |
| `level` | `None`, `Approximate` or `Precise` (enforced by a check constraint) |
| `consent_version` | the version of the explanation the person saw (`LocationSharingConsent.CurrentVersion` = `2026-09-location-v1`) |
| `granted_at_utc` | when the level was chosen (timestamptz, UTC) |
| `revoked_at_utc` | when it was replaced; `NULL` means it is the current row |

How the history works:

- **Append-only.** A change revokes the current row (saved first) and then inserts a new one.
  - A unique partial index `ux_location_sharing_consents_current (user_id) WHERE revoked_at_utc IS NULL` allows at most one current row per person.
  - If the insert fails after the revoke, the person is left at `NONE`, which is the privacy-safe side.
- **Audit trail.** The history rows are the consent audit trail. The existing `AuditInterceptor` also records every insert and update.
- **Default is NONE.** No row means `NONE`. The migration adds no rows, so no existing user gains visibility when this is deployed.
- **The server decides.** The API takes only the caller's own level and the explanation version they saw.
  - A level above `NONE` with an outdated version returns `409 CONSENT_VERSION_OUTDATED`.
  - Turning sharing off never fails on the version.
  - No client flag can influence what someone else sees.
- **API:** `GET` and `PUT /api/v1/me/location-sharing` return `LocationSharingDto(level, consentVersion, grantedAtUtc, currentConsentVersion)`.

## 3. Disclosure levels

| Level | Coordinates | Area (postal code / Gemeinde / city) | Distance |
|---|---|---|---|
| `NONE` | – | – | – |
| `DISTANCE_ONLY` | – | – | band only (`KM_UNDER_1 … KM_OVER_20`) |
| `APPROXIMATE` | ~1 km grid cell (0.01°) | yes | band |
| `PRECISE` | exact | yes | – |

## 4. The central rule

The policy is `SeniorConnect.Domain.LocationVisibilityPolicy.Decide(relationship, ownerConsent)`. The service that applies it is `Profiles.Contracts.ILocationVisibilityService`, implemented by `LocationVisibilityService`. It reads the owner's current consent on every call and never caches it, so a revocation applies to the very next request.

| Relationship (established from server facts) | NONE consent | APPROXIMATE | PRECISE |
|---|---|---|---|
| `Self` (own data) | PRECISE | PRECISE | PRECISE |
| `RequestCreator` (entered this request's location) | PRECISE | PRECISE | PRECISE |
| `AssignedVolunteer` (currently assigned to this request) | APPROXIMATE | APPROXIMATE | **PRECISE** |
| `OrganizationStaff` (coordinator/admin of the request's org) | APPROXIMATE | APPROXIMATE | APPROXIMATE |
| `DiscoveryViewer` (any other user via discovery / open list) | DISTANCE_ONLY | APPROXIMATE | APPROXIMATE |
| `None` | NONE | NONE | NONE |

Precise coordinates are returned only when all of these hold:

1. the requester is authenticated (the endpoint returns 401 otherwise);
2. the requester is authorized for the object: the request exists and they are a party to it, or it is open and they are browsing it; otherwise 404;
3. a business relationship exists (`AssignedVolunteer`);
4. the owner currently has `PRECISE` consent;
5. nothing of higher priority blocks it. None exists yet; see §11.

Things that never produce PRECISE on their own:

- verification;
- organization membership;
- the coordinator role;
- the admin role.

Tests pin this for every combination.

## 5. Help requests

- **Generic `HelpRequestDto`** (get by id, lists, accept, check-in and the other transitions):
  - The mapper caps disclosure at **APPROXIMATE for everyone**, staff included. The generic DTO never carries exact coordinates.
  - Non-parties (the open list and non-party detail) get what the senior's consent allows. With the default consent they get no grid cell and no area.
- **`GET /api/v1/help-requests/{id}/location` → `HelpRequestLocationDto(disclosure, latitude, longitude, postalCode, city)`:**
  - This is the only route to a request's exact coordinates, and the central service decides.
  - Staff get APPROXIMATE.
  - Cross-tenant staff and unrelated users get 404 once the request is no longer open.
- **The visit address (`LocationAddress`):** it stays under the existing rule BR-COMM-04. Only the senior, the creator and the assigned volunteer see it. Staff do not.
  - This is a deliberate decision. The address is the meeting point the requester entered for this visit, and it is needed to carry the visit out.
  - The consent governs coordinates, meaning map-level precision. Product/legal can revisit this (§11).
- **Closed: "staff see exact request coordinates".** Staff used to be treated as a party and received exact `Latitude`/`Longitude`. They now receive at most the grid cell.
  - Proof: `LocationConsentAndVisibilityTests`, tests T05–T07 and T11–T15, plus scenario E.
- **No staff workflow gets exact coordinates.** No current workflow needs them, so none has been added. If dispatch ever needs them, it needs its own `LocationRelationship` and an ADR, not a role check.

## 6. API DTO rules

- Any response type with `Latitude` or `Longitude` must appear in `LocationVisibilityArchitectureTests.ReviewedCoordinateCarriers`, together with whose location it is and who decides. A new type fails the build.
- The reviewed types:

  | Type(s) | Why it may carry coordinates |
  |---|---|
  | `HelpRequestDto` | capped by the policy |
  | `HelpRequestLocationDto` | decided by the policy |
  | `ProximityResult` | null for people and requests; organizations are public venues |
  | `CommunityGroupDto` | personal: grid cell via the policy |
  | `SupportProfileDto`, `VolunteerProfileDto`, `GeocodeResult`, `GeocodeAddressResponse` | the caller's own data |
  | `GemeindeDto`, `NearbyTownDto` | public municipality centroids |
  | `BranchDto` | the public venue of an organization |
  | `NearbyHelpRequestRow` | internal contract, never serialized |

- **Discovery** (`nearby-volunteers`, `nearby-requests`):
  - Returns no user id, no km, no coordinates and a band only.
  - It shows the area only if the owner consented to at least APPROXIMATE. Even PRECISE consent shows only the area, because discovery is not a relationship.
  - The origin is always the caller's stored location; the hotfix test and architecture test still cover this.
- **Community groups:** every group location counts as PERSONAL, because there is no venue marker. It is shown as the ~1 km grid cell through the policy.
- **Organization branches:** these are PUBLIC VENUES and keep exact coordinates. They are business addresses, not homes.
- **Community events:** they carry a free-text venue address that the host published themselves, and no coordinates. See §11.
- **Matching** (candidates, feed, hybrid): distance bands only; no coordinates are involved.
  - Fixed in Phase 3 after the security review: the candidate `DistanceScore` (a continuous number that staff see) had been computed against the senior's *exact* coordinates. With several reference volunteers, staff could have triangulated the home from it.
  - Both ends are now snapped to the grid before any distance is computed (`MatchingService.FindCandidatesAsync`).
  - Test: `Candidates_Scores_RevealNothingFinerThanTheSeniorsGridCell`.
  - Matching authorization is unchanged.

## 7. Geocoder architecture

Backend only: `IGeocodingProvider` → `NominatimGeocodingProvider`, or `UnconfiguredGeocodingProvider`.

- **The fake stub is deleted.** `GeocodingProviderStub`, which answered any address containing "innsbruck" or "kematen" with fixed coordinates, no longer exists.
  - An architecture test allows only these two implementations.
  - With no configuration, every lookup returns **503 `GEOCODING_NOT_CONFIGURED`**. There is no fake fallback in any environment.
- **Provider:** any Nominatim-compatible search API, such as self-hosted Nominatim, OSM or LocationIQ.
  - It calls `GET {BaseUrl}/search?q=…&format=jsonv2&addressdetails=1&limit=1&countrycodes=at&accept-language=…`.
- **Result checks:**
  - The coordinates must be valid, and `IsInAustria` must hold. That check also catches an inverted lat/lon pair.
  - The postal code must be present.
  - The Gemeinde, Bezirk and Bundesland **codes come from our own seed** (the closest centroid for that PLZ), never from the provider's free text.
  - If the address is outside the seeded coverage, the result is `GEOCODE_OUTSIDE_COVERAGE`.
- **Truthful failures:**

  | Code | When |
  |---|---|
  | `GEOCODE_INVALID_ADDRESS` | empty address, or longer than 300 characters |
  | `GEOCODE_NOT_FOUND` | no usable match |
  | `GEOCODE_OUTSIDE_COVERAGE` | the match is outside the seeded coverage |
  | `GEOCODING_TIMEOUT` | the provider did not answer in time |
  | `GEOCODING_UNAVAILABLE` | a 5xx or the network, after retries |
  | `GEOCODING_RATE_LIMITED` | the provider returned 429 |
  | `GEOCODING_NOT_CONFIGURED` | missing configuration, or the provider rejected the credentials (401/403) |

- **Configuration** (`Geocoding` section; the committed files keep `Provider: "None"`):

  | Key | Meaning |
  |---|---|
  | `Provider` | `Nominatim` or `None` |
  | `BaseUrl` | https only; http or a relative URL is refused, so the address never travels in clear text |
  | `ApiKey` | from the **environment only** (`Geocoding__ApiKey`); never in a committed file, never in Flutter |
  | `ApiKeyParameter` | default `key` |
  | `RequireApiKey` | `true` in the committed defaults |
  | `UserAgent` | required by the OSM usage policy |
  | `TimeoutSeconds` | 5 |
  | `MaxRetries` | 1 (for transient failures only) |
  | `MinIntervalMilliseconds` | 1000 (per-instance spacing, as OSM policy requires) |

  Endpoint rate limit: `geocode_policy`, 10 requests per minute per caller.
- **No caching:** geocoding results are never cached, and there is no address cache.

## 8. Logging restrictions

- The geocoder logs only a random correlation id and an outcome or status code.
  - It never logs the address, the URL, coordinates or the key.
  - It never logs `HttpRequestException` text, because that can echo the URI.
- The typed geocoder `HttpClient` is registered with `RemoveAllLoggers()`. Separately, `System.Net.Http.HttpClient` stays at `Warning` in `appsettings.json` and `appsettings.Production.json`; an architecture test checks this.
- These hotfix guards still apply:
  - the source scan finds no `Log*` call carrying coordinate or address identifiers;
  - `Microsoft.AspNetCore` stays at Warning, so request URLs are not logged.

## 9. Coordinate validation

`GeoCoordinate.IsValidPair` requires both values or neither, finite numbers, latitude in [-90, 90] and longitude in [-180, 180]. Otherwise the result is `LOCATION_INVALID_COORDINATES` (400).

- **Where it applies:**
  - help-request creation;
  - support and volunteer profile upserts;
  - `CommunityGroup.Create` and `CommunityGroup.Update`;
  - the geocoder, which also runs the Austria bounds check.
- **Storage is unchanged:** `double`, UTC `timestamptz` for consent, and the existing `(latitude IS NULL) = (longitude IS NULL)` check constraints. No destructive migration.

## 10. Flutter

- **Screen:** `Profile → "Who can see where I live?"`, at `/profile/location-sharing`.
  - It offers three options, each with plain wording that states the actual effect.
  - Choosing PRECISE asks for confirmation first.
  - It has loading, error-with-retry and saved/failed states, in de, en and fa.
- **The server is authoritative:**
  - The state shows only levels the server has confirmed.
  - A failed change keeps the last confirmed level.
  - An outdated explanation reloads the screen.
  - Unknown levels throw instead of being guessed.
  - A network failure produces an error state. It never logs the user out and never produces a made-up level.
- **Nothing to invalidate locally:** the app holds nobody else's exact location, and the Phase 2 reference cache does not accept location data. The server enforces the new state on the very next call.
- **Exact coordinates are parsed in three reviewed files only.** A source-scan test enforces this.
  - geography: the user's own geocode and public centroids;
  - discovery: organization venues; null for people;
  - community group: the grid cell.
- **New error keys:** `errors.location_unavailable`, `location_invalid`, `geocode_not_found`, `geocode_outside_coverage`, `geocoding_unavailable`. The address lookup in profile edit now shows the real reason.

## 11. Residual risks and open decisions

1. **DECISION NEEDED (conflict with a binding rule): the visit address reaches the assigned volunteer without PRECISE consent.** *Phase 4: documented in ADR-026 (Proposed). BR-COMM-04 stays in force; the address now reaches only a **live** assignee and is withheld on a block or safeguarding contact restriction. The consent question itself is still open.*
   - BR-COMM-04 (`docs/product/business-rules.md`) requires that the address is revealed after a confirmed assignment to the assigned counterparty. The Phase 3 prompt asks that precise location need explicit consent.
   - The street address is precise location.
   - AGENTS.md forbids silently changing a binding rule, so the address still follows BR-COMM-04; only the *coordinates* need PRECISE consent.
   - The security review rated this HIGH.
   - Options for an ADR:
     - (a) keep BR-COMM-04 and treat entering the visit address on the request as purpose-bound consent;
     - (b) require PRECISE consent for the address too, which breaks home visits for every existing senior until they opt in;
     - (c) ask for consent at assignment time.
2. **No staff workflow gets exact coordinates.** If field dispatch needs them, add a dedicated relationship and an ADR.
3. **Community event venue addresses** are free text published by the host. A private host could publish their own home. *Closed in Phase 4 (§12).*
4. **There is no safeguarding exception.** Higher-priority blocks, such as a safeguarding hold or a user block, are not yet an input to the policy. *Closed in Phase 4: blocks and safeguarding location restrictions are inputs (§14); there is still no exception that grants access.*
5. **Rate-limit spacing is per instance.** With several API instances the provider sees N× the rate. Use a shared limiter or a self-hosted Nominatim if the API scales out.
6. **Other clients' copies.** After a revocation, a volunteer's app may still hold coordinates it fetched earlier. The server stops disclosing immediately, but it cannot recall data already delivered. *Reduced in Phase 4 (§11a): the app never persists coordinates; a copy lives only in memory while a screen shows it.*
7. **Coordinates are stored in plaintext.** Database encryption is out of scope.
8. **The owner can re-query from a moved home.** The hotfix's residual risk 5 still applies: the leak is bounded to one ~1 km cell.
9. **The Gemeinde seed is partial.** Addresses outside the sample seed return `GEOCODE_OUTSIDE_COVERAGE`. Seeding all 2,093 Gemeinden is a separate data task.

---

# Phase 4 additions (2026-09-25)

## 11a. Revocation and local copies

- The server side is unchanged: consent is read fresh on every request, so a
  revocation denies the next request (T21, scenario E).
- On the device, `LocalReadCache.save` strips every `latitude`, `longitude`,
  `lat`, `lng` and `lon` key before anything is written to shared preferences
  (T22).
- The reference cache holds only public municipality centroids.
- No Flutter code calls `/help-requests/{id}/location` yet. Coordinates in DTOs
  stay in widget or notifier memory and disappear with the screen. There is no
  offline sync of help requests.

## 12. Community event venues

`CommunityEvent.VenueType` is `Private` (the default, and the value of every
existing row after the migration) or `Public`.

| Viewer | Private venue | Public venue |
| --- | --- | --- |
| Host, group manager, staff of the event's organization | address | address |
| Attendee with RSVP `Going` (detail and my-schedule) | address | address |
| Anyone else, lists | postal code only | address |

- **Only organization events may declare a public venue.** An individual host
  cannot publish their home as public (`EVENT_PUBLIC_VENUE_REQUIRES_ORGANIZATION`,
  T33), neither on create nor on edit.
- Discovery (`CommunityEventDiscoveryRow`) never carried the address.
- **Client migration.** Clients that do not send `venueType` create private
  events. Organizations that want an open venue must send `"venueType": "Public"`.

## 13. DTO classification

`LocationVisibilityArchitectureTests.EveryTypeWithALocationField_IsClassified`
finds every public DTO or contract type with a `Latitude`, `Longitude`,
`*Address*`, `*PostalCode*`, `*Gemeinde*`, `*Municipality*` or `*Distance*`
property. Each one must be classified as `PUBLIC`, `APPROXIMATE`,
`DISTANCE_ONLY`, `PRECISE_AUTHORIZED`, `PRIVATE_INTERNAL` or `OWN_DATA`. A new
type fails the build until it is reviewed. The sweep found one real gap: the
volunteer feed (`VolunteerFeedItem`) returned every senior's postal code and
city regardless of consent. It now asks the central policy, so the area shows
only with the senior's consent (at least APPROXIMATE).

## 14. Policy inputs added

`ILocationVisibilityService.DecideAsync(owner, viewer, relationship)` now takes
the **viewer**. The disclosure is `NONE` when owner and viewer are blocked in
either direction, or a safeguarding `LOCATION_BLOCKED` restriction applies to
them. Nothing lifts this: not verification, not organization membership, not a
staff or officer role.

The `AssignedVolunteer` relationship exists only while the assignment is live
(`Assigned` or `InProgress`). After cancellation, completion or a no-show, the
former volunteer is a plain party with at most the grid cell (T36).

