# Reference-data cache (Phase 2)

> Status: implemented 2026-09-25. Scope: a lightweight read-through cache for
> public, slow-changing reference lists, plus the municipality (Gemeinde) API
> contract fix. It is **not** an offline mode, it does no synchronization, and
> it adds no new storage package.
>
> Related: `docs/security/location-and-reference-data-design.md` (Phase 1 audit,
> finding F-REF-1), `docs/security/hotfix-matching-location.md` (the hotfix,
> which is unaffected by this work).

## 1. Why this exists

The server is the source of truth. The cache only stops the app from
downloading the same lists again every time a screen opens or a dropdown is
used.

## 2. Classification

These are all the Flutter call sites that load relatively static data.

| Endpoint | Consumers | Class | Cached |
|---|---|---|---|
| `GET /api/v1/reference/austria/bundeslaender` | profile edit (address) | A · REFERENCE_STATIC | yes, 30 days |
| `GET /api/v1/reference/austria/bezirke?bundeslandCode=` | profile edit | A · REFERENCE_STATIC | yes, 30 days, **wiped at logout** |
| `GET /api/v1/reference/austria/gemeinden?bezirkCode=` | profile edit | A · REFERENCE_STATIC | yes, 30 days, **wiped at logout** |
| `GET /api/v1/reference/austria/lookup?plz=` | profile edit (PLZ auto-fill) | C · USER_SPECIFIC (see below) | **no** |
| `GET /api/v1/activities/categories` | senior request flow, request status, active assignment, log activity, coordinator bulk entry | B · REFERENCE_SLOW_CHANGING | yes, 24 hours |
| `POST /api/v1/reference/geocode-address` | profile edit | D · SENSITIVE_OPERATIONAL (a home address with coordinates) | **no** |
| `GET /api/v1/activities/referral-providers` | none in Flutter | not assessed for caching; there is no consumer | no |

Notes:

- **Why the PLZ lookup counts as user-specific.** The response is public, but
  the query is the postal code the user types, which is usually their own. The
  cache key would record it (`…geo.plz.6020`) and outlive the session. Under the
  rule "if in doubt, USER_SPECIFIC", it is not cached.
- **Why Bezirk and Gemeinde entries are wiped at logout.** They are keyed by
  what was opened (`…geo.gemeinden.701`), which hints at where the last user
  lives. Their payload is public, so they are cached during a session. The
  unparameterized lists (Bundesländer, categories) survive logout.
- **Gemeinde latitude/longitude** is the public centroid of the municipality
  from the administrative seed. It is not anyone's location.
- **Activity categories** are global: they are not tenant- or user-specific
  (`ActivityCategory` is loaded reference data). Whether a category is blocked
  is enforced again by the server when a request is created
  (`HelpRequestService`: `CategoryBlocked`), so a cached `isBlocked` that is up
  to 24 hours stale cannot let a blocked request through.
- **No "representatives" or contact list** exists in the app today, so none is
  cached.

## 3. Never cached

- help requests
- assignments
- matching results
- messages
- safeguarding
- emergency/SOS
- senior, profile or organization-private data
- any location of a person
- precise coordinates or geocoding results
- tokens

This is enforced in three ways:

1. **Closed allowlist.** `ReferenceDataset` (in
   `mobile/senior_connect/lib/core/cache/reference_data_repository.dart`) is an
   enum. Nothing outside it can be cached, and there is no generic "cache this
   response" API.
2. **Payload guard.** A response that contains any field in
   `kForbiddenReferenceFields` is returned to the caller but never stored. The
   fields are `userId`, `displayName`, `addressLine`, `phone`, `email`, the
   token fields, `helpRequestId`, `assignedVolunteerId` and
   `safeguardingCaseId`.
3. **Backend contract test.** `ReferenceDataContractTests` pins the JSON shape
   of every cached DTO and fails if one gains a personal, tenant or session
   field.

## 4. Design

```
UI / notifier
    └─ GeographyRepositoryImpl / ReferenceDataRepository.activityCategories()
          └─ ReferenceDataRepository.load(dataset, path, parse, param?)
                ├─ SharedPreferences  (key reference.v1.<dataset>[.<param>])
                └─ ApiClient.get       (server, authoritative)
```

The UI never calls HTTP for these lists. Storage uses the existing
`shared_preferences` dependency, the same infrastructure as `LocalReadCache`,
but under its own `reference.` prefix. `LocalReadCache`'s
`senior_connect_cache_` namespace (session data, wiped at logout) is untouched.

Each entry stores:

```json
{"v": 1, "storedAt": <epoch ms>, "payload": [ ... server list verbatim ... ]}
```

- **Cache key:** `reference.v<version>.<dataset>[.<param>]`.
- **Cache parameters:** a parameter becomes a key segment only if it matches
  `^[0-9A-Za-z]{1,12}$`. Anything else is fetched without being cached.
- **Schema version:** `kReferenceCacheVersion`, carried in both the key and the
  entry.

## 5. Policy

| Situation | Behaviour |
|---|---|
| No entry | Request the server, parse, store, return. |
| Fresh entry (age < TTL) | Return the cache. No request. |
| Stale entry | Request the server. On success, replace the entry. |
| Stale entry + temporary failure (timeout, connection error, 5xx) | Return the stale entry. |
| Stale entry + other failure (4xx, e.g. 401) | Rethrow. Stale data never masks an auth or validation error. |
| No entry + failure | Rethrow. The screen shows its existing truthful error state. |
| `refresh: true` | Skip a fresh entry and ask the server. |

**No fake data.** Nothing is ever invented. A missing field throws; it is never
defaulted. The only fallback is a real earlier server response.

**TTL:**

- geography: 30 days (the administrative units change a few times a decade);
- activity categories: 24 hours.

**ETag / `If-None-Match` / 304: not implemented.** The API sends no ETag, and
Dio treats 304 as an error by default, so adding it means work on both the
server and the client for little gain on lists this small. TTL expiry is
enough for the current deployment. Add it if the lists grow or the TTL has to shrink.

## 6. Invalidation

| Trigger | Mechanism |
|---|---|
| Cache/schema version bump | Entries with another `v` are ignored and deleted on read. Keys of other versions are purged on the next write. |
| Explicit refresh | `load(..., refresh: true)`, `ReferenceDataRepository.invalidate(dataset)`, `invalidateAll()` |
| Parsing fails | The cached entry is deleted, then the server is asked. A server payload that fails to parse is not stored. |
| Corrupt payload | Undecodable JSON is deleted and treated as a miss. |
| Logout | `ApiClient.clearTokens` calls `ReferenceDataRepository.clearSessionTraces()`, which drops the Bezirk and Gemeinde entries. |

Refreshing reference data never clears other application storage.

## 7. Logout and security boundaries

On logout, `LocalReadCache.clearAll()` removes session data. Bundesländer and
activity categories remain because they are public and not user-specific.
Nothing user-specific is ever written under `reference.`. Tokens stay in
`flutter_secure_storage` and never touch this cache.

## 8. Municipality API contract (F-REF-1): fixed

Tracing the contract from backend to Flutter showed the bug:

- **Backend.** `GemeindeDto(string Code, string Name, string PostalCode, double
  Latitude, double Longitude)` goes through ASP.NET web JSON defaults (camelCase)
  and serializes as `{"code","name","postalCode","latitude","longitude"}`.
- **Flutter.** `Gemeinde.fromJson` read `gemeindeCode` and `gemeindeName` with
  `as String`. That threw a `TypeError` on every real response.
- **Effect.** `getGemeinden` (the Gemeinde dropdown) and `lookupByPlz` (PLZ
  auto-fill) were broken at runtime. The PLZ failure was swallowed silently by
  the screen.

**Canonical contract:** the backend's `code`/`name`. It matches `BundeslandDto`
and `BezirkDto`, and it is the deployed API. Only the Flutter model changed. No
backwards-compatibility parsing was added, because no deployed client ever
parsed the response successfully.

`GeocodeResult` legitimately uses `gemeindeCode`/`gemeindeName` on both sides
and is unchanged.

**Municipality data is unchanged.** The seed still covers a sample region
only. Seeding all 2,093 Gemeinden is a separate deployment/data task (P1-15b
and ADR-021), and no records were invented.

## 9. Tests

**Flutter** (`test/reference_data_cache_test.dart`, 20 tests):

- first load;
- no request within the TTL;
- stale refresh;
- explicit refresh and invalidation;
- stale fallback when offline;
- a 4xx is not masked;
- a truthful error with no cache;
- corrupt and unparseable entries are discarded;
- a version bump invalidates;
- the allowlist;
- logout (session traces wiped, nothing user-specific left);
- geocoding and addresses are not stored;
- help requests are not stored;
- safeguarding data is not stored;
- unsafe parameters are not cached;
- PLZ lookups are not cached;
- `getGemeinden` and `lookupByPlz` parse the real backend shape and keep the
  code;
- missing or old field names throw.

**Backend** (`tests/architecture/.../ReferenceDataContractTests.cs`, 7 tests):
the JSON field lists for Gemeinde, Bundesland, Bezirk and ActivityCategory, and
no personal, tenant or session fields on any of them.

## 10. Known limitations

- There is no ETag or 304 revalidation (see §5).
- There is no in-memory layer. Each read decodes JSON from SharedPreferences,
  which is fine for these small lists.
- A stale fallback is not flagged to the UI. It is public reference data, so
  the screens behave as if it were fresh.
- Activity categories can be up to 24 hours stale on the client. The server
  still enforces blocked categories.
- The Gemeinde seed is partial. That is a data task, not covered here.
- `referral-providers` has no Flutter consumer and was not classified for
  caching.
