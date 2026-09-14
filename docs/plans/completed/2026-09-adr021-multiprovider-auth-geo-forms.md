# ADR-021 Implementation — Multi-Provider Auth · Austrian Geography · Dynamic Intake Forms

Status: **active** · Started: 2026-09-12
References: `ADR-021` (accepted), `BR-AUTH-*`, `BR-GEO-*`, `BR-ORG-FORM-*`, `BUILD-CHECKLIST.md` update #4.

## Goal

Implement the ADR-021 scope, i.e. the tasks the checklist labels `P1-08a`, `P1-08b`,
`P1-13b`, `P1-15b…e`, `P1-27`, `P1-28`, and `P2-37…41`, backend first.

## Non-goals

- Flutter screens `P1-27`/`P1-28` and `P2-40`/`P2-41` in this slice (planned, next).
- Real Google / ID Austria / BEV-Nominatim credentials. All external providers are
  behind adapters, exactly like the existing `ISmsSender`/`IEmailSender` stubs.
- PostGIS activation (`P8-01`); we use Haversine in C#, consistent with ADR-005.

## Actors

- Seniors / support recipients (email+password, Google, ID Austria, phone OTP)
- Volunteers / independent volunteers
- Organization coordinators (intake form builder, Phase 2)
- Platform staff

## User journey

J1 (onboarding): any of the four auth paths → same `users` table → trust level
computes from verified phone/email → SupportProfile/VolunteerProfile creation.
J2 (location): PLZ/Gemeinde auto-complete → address lookup on map → confirm pin →
coordinates persisted → proximity/discovery lists show fuzzed locality + distance.

## Business rules applied

- `BR-AUTH-01..10` (multi-provider, email verify 24h, ID-Austria trust elevation,
  in-profile phone verification, enforcement for unverified accounts)
- `BR-GEO-01..06` (Austrian directory, cascading autocomplete, geocoding + map pin,
  proximity, mutual discovery, location fuzzing)
- `BR-AUTH-06`/`BR-AUTH-07` (SIM-swap, OTP rate limiting) — unchanged behaviour

## Phase 1 backend slice (this work)

All backend tasks below stay inside the **Identity** module plus a new
**Geography** module (reference data + discovery), both wired into the existing
`SeniorConnectDbContext` composite.

### Identity module changes

- `AuthMethod` enum gains `Google`, `IdAustria`, `EmailPassword`.
- New entities: `UserExternalLogin` (provider, provider_key, email, display_name,
  linked_at_utc), `EmailVerificationToken` (token_hash, expires_at_utc,
  used_at_utc). Migration `20260912_Adr021_Multiprovider_Auth`.
- `User` factories: `CreateWithEmailPassword`, `LinkExternalLogin`.
- `IIdentityService` additions:
  - `RegisterEmailPasswordAsync` (validates matching confirm, dispatches 24h token)
  - `VerifyEmailRegistrationTokenAsync`
  - `EmailPasswordLoginAsync`
  - `LoginWithGoogleAsync` (ID-token validation via adapter)
  - `LoginWithIdAustriaAsync` (OIDC code exchange via adapter)
  - `RequestProfilePhoneVerificationAsync` / `VerifyProfilePhoneAsync`
- `OtpPurpose` gains `PhoneVerification`.
- New gates: unverified email accounts may not create announcements, apply to
  organizations, or accept assignments (`BR-AUTH-10`) — applied at the application
  layer where those mutation handlers resolve capabilities.
- Provider adapters: `IGoogleIdTokenValidator`, `IIdAustriaClient`
  (implementations are stubs in dev, mirroring `SmsSender`).

### New Geography module (`backend/modules/Geography/`)

- Entity `AustrianAdministrativeUnit` (bundesland_code/name, bezirk_code/name,
  gemeinde_code/name, postal_code, locality_name, latitude, longitude, is_active).
- Services: `IGeocodingProvider` (stub), `IProximityService` (Haversine),
  `IStoreLocationService` (persists geocoded coords + `austrian_gemeinde_code`/`address_verified`/`geocoded_at_utc` to Support/Volunteer profiles via their owning module's Application interface), `IDiscoveryService`.
- Endpoints (group `/api/v1/reference/austria` and `/api/v1/discovery`):
  - `GET /reference/austria/bundeslaender`
  - `GET /reference/austria/bezirke?bundeslandCode=`
  - `GET /reference/austria/gemeinden?bezirkCode=`
  - `GET /reference/austria/lookup?plz=`
  - `POST /reference/geocode-address`
  - `GET /discovery/nearby-organizations` (auth)
  - `GET /discovery/nearby-volunteers` (auth)
  - `GET /discovery/nearby-requests` (auth, volunteers)
- Seed data: all 9 Bundesländer + pilot-region Bezirke/Gemeinden/PLZ (Tyrol,
  incl. 6175 Kematen, Zirl, Völs, Innsbruck). Community-driven extension later.

## API contract (Phase 1 slice)

- `POST /api/v1/auth/register`  → 201 `{ userId }`; dispatch verification email
- `GET  /api/v1/auth/verify-email?token=` → 204; marks `email_verified_at_utc`
- `POST /api/v1/auth/login` (email+password) → `AuthResponse`
- `POST /api/v1/auth/google` → `AuthResponse`
- `POST /api/v1/auth/id-austria` → `AuthResponse`
- `POST /api/v1/me/phone/request-verification` (auth) → 200
- `POST /api/v1/me/phone/verify` (auth) → 204; sets `phone_verified_at_utc`
- `GET  /api/v1/reference/austria/*` listed above (no auth)
- `POST /api/v1/reference/geocode-address` (auth) → resolved pin preview
- `GET  /api/v1/discovery/*` (auth) — fuzzed locality + distance

Error codes: `EMAIL_ALREADY_REGISTERED`, `PASSWORDS_DO_NOT_MATCH`,
`EMAIL_VERIFICATION_REQUIRED` (BR-AUTH-10), `PHONE_VERIFICATION_REQUIRED`,
`GEOCODE_NOT_FOUND`, `EXTERNAL_LOGIN_FAILED`.

## Authorization matrix (Phase 1 slice)

| Endpoint | Anonymous | Authenticated | Staff |
|---|---|---|---|
| auth/* (register/login/google/id-austria) | ✓ | — | — |
| me/phone/* | ✗ 401 | ✓ | ✓ |
| reference/austria/* | ✓ | ✓ | ✓ |
| reference/geocode-address | ✗ 401 | ✓ | ✓ |
| discovery/* | ✗ 401 | ✓ (own radius) | ✓ |

No cross-tenant rows are read in this slice; discovery reads only public/branch and
profile data filtered by radius, with exact lat/lng fuzzed (BR-GEO-06).

## Trust / safety implications

- ID Austria login adds an `Identity` verification record (provider `IdAustria`,
  verified) → trust levels 1/2 per BR-AUTH-08. Duplicated providers idempotent.
- Phone/email verification is server-side only; never trusted from client.
- Discovery never reveals exact street addresses/coordinates before assignment.

## Failure cases

- Google ID token invalid → 401 `EXTERNAL_LOGIN_FAILED`
- ID Austria state/Nonce mismatch → 401
- Email verify token expired/used → 409 `TOKEN_INVALID_OR_EXPIRED`
- Registering with an existing email → 409 `EMAIL_ALREADY_REGISTERED`
- Geocode no hit → 409 `GEOCODE_NOT_FOUND`
- Phone OTP exhausted → 409 `OTP_EXHAUSTED` (existing behaviour)

## Concurrency concerns

- External login linking: unique `(provider, provider_key)` index; conflict → reuse.
- Email verification token: single-use, consumed in the same transaction.

## Audit requirements

- Login, phone verification, external login link, and email verification are
  auditable events (BR-AUDIT-01). Audit entries written at service layer via the
  existing `AuditEntry` recorder.

## Test plan

- Unit: `User` external-link factory, `EmailVerificationToken` single-use, match-password
  validation, proximity Haversine math.
- Integration (in-memory, following `IdentityServiceIntegrationTests`):
  register → token → verify → login; google/id-austria stub flows; profile phone
  verify; PLZ 6175 lookup → Kematen; geocode stub; discovery radius + fuzzing.
- Negative: 401 unauthenticated on me/phone + discovery; wrong confirm password
  400; BR-AUTH-10 gate for unverified email on onboarding apply.

## Open questions / risks

- Real third-party credentials deferred (stubs log and return success).
- Full 2,093-Gemeinden dataset not bundled; pilot region (Tyrol) seeded now.

## Build order

1. Identity domain + contracts + db context (migration)   → P1-08b, P1-13b, P1-08a
2. Geography module (domain + seed + endpoints)            → P1-15b..e
3. Flutter auth/profile screens                            → P1-27, P1-28
4. Org intake forms (Phase 2)                              → P2-37..41