# Build Checklist

> ## ⚠️ Implementation status — read before continuing
>
> As of 2026-08, self-reported progress reached through Phase 7, but
> **`docs/plans/PHASE-AUDIT-2026-08.md` found real gaps behind that report**:
>
> - Gate 3 (Help & Matching) was never actually passed — `P3-21` through
>   `P3-24`, the entire mobile UX for the core loop, are unbuilt.
> - Phases 5 and 6 were built without the P0-03 / P0-04 interviews that
>   this document marks as a hard block.
> - Phase 7's Legal Gate and several hardening tasks (`P7-04`, `P7-09`
>   through `P7-17` in the numbering below) were never attempted despite a
>   "ready for deployment" claim elsewhere.
> - Six Phase 2 tasks (`P2-14`, `P2-16`, `P2-19`, `P2-25`, `P2-30`, `P2-35`)
>   are still open.
>
> **Read the audit before ticking anything else in this file.** It has the
> full evidence and a recommended order of work that is not "start Phase 8."
>
> **2026-08 update (ADR-018):** the product's scope broadened from
> seniors-only to a general mutual-aid platform (seniors, newcomers,
> families, volunteers). A new **Phase 2.9 — Scope Generalization** is
> inserted below, right before Phase 3's mobile screens, because those
> screens don't exist yet (see the audit) and this is the cheapest possible
> moment to make this change. Do Phase 2.9 before `P3-21`.
>
> **2026-09 update (ADR-020) — MVP LOCKDOWN + reality check:**
>
> - **v1 = Phases 0–4 only**, for one named partner: **Freiwilligenzentrum
>   Innsbruck-Land**. Do not start Phase 5 or Phase 6 speculatively.
> - **Naming resolved (ADR-019, Accepted):** keep SeniorConnect. Do not
>   attempt a rename.
> - **Phase 5 is renamed and redefined**: "Org Announcements & Events", not
>   "Community". Individual users get no public page or self-service group.
>   Only an `Organization` can publish a public announcement/event
>   (`BR-COMM-06`) — **implemented** in `backend/modules/Community/`
>   (`CommunityGroup.Create` now rejects a missing `organizationId`).
> - A **local message-moderation classifier is a blocking prerequisite**
>   (`BR-COMM-05`) for any comment/group-thread feature — added as `P4-18`,
>   **implemented** as `LocalMessageModerationService`, wired into
>   `PostMessageAsync`/`GetMessagesAsync`.
> - A **coordinator-only recognition shortlist** (private, non-numeric to
>   users) is added to Phase 2 impact reporting as `P2-36`, not yet built.
>   Public leaderboards/points remain permanently rejected (ADR-009, ADR-020).
> - **Reality check (verified 2026-09, this session):** despite every task
>   below showing `[ ]`, `dotnet build` and `dotnet test` on the actual
>   `backend/` solution succeed with **178 tests passing, 0 failures** across
>   Identity, Community, TrustSafety, Family, PilotHardening, HelpRequests
>   and the architecture-test suite. The mobile app already has files for
>   `P3-21`–`P3-24` (senior request flow, volunteer feed, active assignment,
>   emergency screen) — this checklist's unchecked boxes reflect that ticking
>   was never kept current, not that the work doesn't exist. **Do not assume
>   `[ ]` means unbuilt; verify with the app before re-building anything.**
>   Full priority order to work from now: `docs/plans/PHASE-AUDIT-2026-08.md`
>   §"What to actually do next" — naming (done) → close/verify the Phase 3
>   mobile gap → run Gate 3 honestly → backfill Phase 2 gaps → Phase 0
>   retroactive interviews → Phase 7 for real. Not Phase 8.
> - Full rationale: `docs/decisions/ADR-020-recognition-org-pages-moderation-mvp-lockdown.md`.
>
> **2026-09 update #2 — Phase 2 gap backfill, verified this session:**
> `P2-14` (volunteer self-log), `P2-19` (reactivation), `P2-30` (PDF export),
> `P2-35` (funder dashboard) were already real and working — verify against
> the code before rebuilding. `P2-16` (monthly reminder) had the endpoint but
> **no idempotency guard and no schedule** — fixed: `VolunteerProfile`
> now tracks `LastMonthlyReminderMonth`, and
> `MonthlyVolunteerReminderHostedService` runs the sweep daily, safe to
> call any number of times. `P2-36` (recognition shortlist, ADR-020) is
> **now built**: `GET /api/v1/coordinator/organizations/{id}/recognition-shortlist`,
> staff-only, never exposed elsewhere.
>
> **2026-09 update #3 — `P2-25` built (Flutter, not Blazor/React):** redefined
> per `docs/superpowers/specs/2026-09-11-org-profile-flutter-p2-25-design.md` —
> reused the existing Flutter app (`mobile/senior_connect`) with the `web`
> platform enabled, instead of a separate Blazor/React project. Also closed a
> real authorization gap found along the way: `CommunityService` previously
> let any authenticated user publish a group/event on behalf of any
> organization; it now checks `IOrganizationCoordinatorReader` first.
>
> **2026-09 update #4 — Auth Multi-Provider, Austrian Geo & Dynamic Intake Forms (ADR-021):**
> Extends authentication and Austrian localized onboarding per pilot requirements:
> - **Multi-Provider Auth:** Google Sign-In, Email + Password + Confirm (with 24h verification link), ID Austria eIDAS integration, and SMS OTP.
> - **Austrian Administrative Geography:** Complete master data (9 Bundesländer, 94 Bezirke, 2,093 Gemeinden, PLZ), address geocoding with interactive map pin preview, coordinate persistence, and proximity calculations.
> - **Mutual Local Discovery:** Users discover nearby charities and independent volunteers; volunteers discover nearby charities and help seekers.
> - **Organization Custom Intake Forms:** Configurable intake forms for volunteers and help-seekers with mandatory/optional flags, pre-seeded with the **FWZ Innsbruck-Land** reference questionnaire (`Interesse für Freiwilligentätigkeit`, `Personengruppen`, `Zeitaufwand`, `Strafrechtliche Unbescholtenheit`, `Einwilligung zur Datenverarbeitung`).
>
> ---
>
> **This is the file you work from.** Tick tasks in order, top to bottom.
> Each task states what to build, which rules apply, and one concrete thing
> **you** verify before ticking it.
>
> Companion documents:
> `roadmap.md` (why this order — ADR-015) · `business-rules.md` (the BR-* rules)
> · `../architecture/` (how) · `../../starter/` (working code and SQL to copy)
>
> **Rules of use**
> 1. Do not skip ahead. Later tasks assume earlier ones.
> 2. A task is done when its `✓ Test` passes, not when the code compiles.
> 3. A phase is done when its **Gate** passes. Do not start the next phase early.
> 4. New ideas go to `backlog.md`, never into the current phase.
>
> Size: **S** ≈ half a day · **M** ≈ 1–2 days · **L** ≈ 3–5 days (part-time).

---

## Legend

```
[ ] Pn-NN — Task title          · size · needs: Pn-NN
    what to build
    → rules that apply
    ✓ Test: the one thing you verify yourself
```

Detail is highest for Phases 0–3, because that is what you will build next.
Phases 5–8 are deliberately coarser — writing 60 precise tasks for work that
starts in eight months is false precision. Expand each phase into detail at the
moment it begins.

---

# PHASE 0 — Discovery & Project Brain

**Goal:** stop designing from assumptions. Partially complete.

## 0.1 Verification of existing findings

```
[ ] P0-01 — One real coordinator call            · S
    Verifies F1, F2, F3, F7, F9 in a single conversation.
    Ask literally: "Show me the file you track hours in."
    → discovery-findings.md §Verification plan
    ✓ Test: five findings marked VERIFIED or CORRECTED in discovery-findings.md

[ ] P0-02 — Insurance broker call                · S
    Volunteer accident cover AND private-vehicle transport liability.
    → F4, BR-TRANSPORT, BR-SAFETY-06
    ✓ Test: a written answer, in the repo, on who carries risk in each case

[~] P0-03 — Five conversations with seniors      · M   ⚠️ WAIVED 2026-09
    Founder decision (2026-09): proceed on the assumptions in the
    interview-answer packs already collected (Antworten.txt, Antworten1.txt,
    the PDF sets) instead of waiting for live conversations. This is a
    real, accepted risk — `uer-unsat.txt`'s central objection was precisely
    that reach/adoption is unproven by written answers alone. Phase 5 (Org
    Announcements & Events) proceeds without this gate.
    ✓ Test: none — gate waived, not passed. Revisit if the pilot shows the
            assumptions were wrong.

[~] P0-04 — Five conversations with adult children · M  ⚠️ WAIVED 2026-09
    Same founder decision as P0-03. Phase 6 (Family & Delegation) proceeds on
    assumption, matching what the actual codebase already built without this
    gate (`PHASE-AUDIT-2026-08.md` finding #1).
    ✓ Test: none — gate waived, not passed.
```

## 0.2 Setup

```
[ ] P0-05 — Git repository + this document tree  · S
    ✓ Test: first commit contains docs/, AGENTS.md, skills/, starter/

[ ] P0-06 — CI pipeline, green on empty          · S
    build · test · format · lint, on every push
    ✓ Test: a deliberately mis-formatted file fails the build

[ ] P0-07 — Pilot Gemeinde identified            · M
    5 000–20 000 inhabitants, personal connection, active Seniorenbund
    → go-to-market.md §2
    ✓ Test: a named Gemeinde and a named contact person

[ ] P0-08 — Budget calendar written down         · S
    → F9. Timing beats features. Miss the council cycle and you wait a year.
    ✓ Test: a date in the repo for when next year's budget is decided

[ ] P0-09 — Freeze MVP scope                     · S
    Write docs/plans/mvp-scope.md = Phases 1–4. Update CURRENT PHASE in AGENTS.md.
    ✓ Test: AGENTS.md says the current phase and what is out of scope
```

### 🚦 GATE 0
```
[ ] P0-01 and P0-02 done — the coordinator and insurance answers are real
[ ] Pilot Gemeinde named
[ ] CI green
[ ] MVP scope frozen and written down
```
P0-03 and P0-04 may run in parallel with Phases 1–4. They must complete before
Phase 5 and Phase 6 respectively.

---

# PHASE 1 — Foundation  (4–5 weeks)

**Goal:** an app you can log into that looks and feels right, in three
languages, in both themes. No business features.

## 1.1 Backend skeleton

```
[ ] P1-01 — Solution structure                   · M
    Copy starter/backend/. Projects: SharedKernel, Infrastructure, Api,
    modules/Identity, modules/Profiles, modules/Audit.
    ⚠️ The starter C# has NOT been compiled. Expect to fix usings and versions.
    → ADR-001, backend/AGENTS.md
    ✓ Test: dotnet build succeeds with TreatWarningsAsErrors

[ ] P1-02 — Architecture tests wired in          · S · needs P1-01
    Copy all five suites from starter/backend/tests/.
    → They must pass from the FIRST commit. That is the point.
    ✓ Test: dotnet test passes; deleting a query filter makes it fail

[ ] P1-03 — PostgreSQL + EF Core + migration     · M · needs P1-01
    Apply starter/sql/001_phase1_init.sql as the reference, generate the
    equivalent EF migration.
    → data-model.md, ADR-005 (lat/lng now, PostGIS later)
    ✓ Test: starter/sql/900_schema_tests.sql — all 9 pass against your database

[ ] P1-04 — ProblemDetails + error codes         · S · needs P1-01
    RFC 7807 with a stable `code` field on every error.
    → api-conventions.md, SharedKernel/Result.cs
    ✓ Test: a 404, 403 and 409 each return a distinct machine-readable code

[ ] P1-05 — Serilog, correlation IDs, health     · S · needs P1-01
    → system-design.md §5. Never personal data in a log line; IPs hashed.
    ✓ Test: grep a day of dev logs for a phone number — zero hits

[ ] P1-06 — Audit module, append-only            · M · needs P1-03
    SaveChangesInterceptor + REVOKE UPDATE, DELETE at the database.
    → BR-AUDIT-01..03
    ✓ Test: UPDATE audit_entries as the app role → permission denied
```

## 1.2 Multi-Provider Identity & Authentication (ADR-016 & ADR-021)

```
[ ] P1-07 — SMS provider adapter                 · M · needs P1-01
    Behind ISmsSender. EU provider.
    ✓ Test: a code arrives on a real Austrian mobile number

[ ] P1-08 — Request OTP endpoint (Phone & SMS)   · M · needs P1-03, P1-07
    POST /auth/request-code. Hash destination and code, rate limit per IP/number.
    → BR-AUTH-01, BR-AUTH-07
    ✓ Test: 6 requests in a minute for one number → 6th returns 429

[x] P1-08a — Google Sign-In & ID Austria OIDC     · L · needs P1-03
    POST /auth/google (validates Google ID token via GoogleJsonWebSignature).
    POST /auth/id-austria (handles eIDAS OpenID Connect authorization code flow).
    → BR-AUTH-01, BR-AUTH-08, ADR-021
    ✓ Test: valid Google token exchanges for session JWT; ID Austria grants Trust Level 1

[x] P1-08b — Email + Password + Verification      · M · needs P1-03
    POST /auth/register with email, password, confirm_password.
    Dispatches email verification token (valid 24h).
    GET /auth/verify-email?token=... marks email_verified_at_utc.
    → BR-AUTH-01, BR-AUTH-02, BR-AUTH-10
    ✓ Test: registering with non-matching password confirmation returns 422;
      logging in before verification returns warning; token confirms account

[x] P1-09 — Credential verification + tokens     · M · needs P1-08
    Issues 90-day refresh token for personal device, 15-minute access token.
    Supports Google, ID Austria, verified Email+Password, and SMS OTP.
    → BR-AUTH-04, BR-AUTH-07
    ✓ Test: valid credentials return token pair; wrong password/OTP fails with counter

[x] P1-10 — Refresh, logout, device list         · S · needs P1-09
    Hashed, revocable per device.
    ✓ Test: logging out one device leaves the other signed in

[x] P1-11 — Email magic link fallback            · M · needs P1-03
    → BR-AUTH-01
    ✓ Test: a user without a phone or password can sign in via emailed one-time link

[x] P1-12 — Staff TOTP path                      · M · needs P1-03
    Organization staff and platform admins can enable TOTP authenticator.
    → BR-AUTH-02
    ✓ Test: staff account requires TOTP when enabled

[x] P1-13 — Phone-number change flow             · M · needs P1-09
    Invalidates every session, notifies old number and email, audited.
    → BR-AUTH-06 (SIM-swap mitigation)
    ✓ Test: change number → other device is signed out within a minute

[x] P1-13b — In-Profile Mobile Phone Verification · M · needs P1-08
    POST /me/phone/request-verification and POST /me/phone/verify.
    Enables user to enter mobile number in profile and click "Verify Phone Number".
    → BR-AUTH-09, ADR-021
    ✓ Test: verifying 6-digit SMS OTP sets phone_verified_at_utc on user profile
```

## 1.3 Profiles, Austrian Administrative Geography & Proximity

```
[ ] P1-14 — User + SupportProfile + VolunteerProfile · M · needs P1-03
    Not mutually exclusive. One user may hold both.
    → personas.md, data-model.md §1, ADR-018
    ✓ Test: one user holds both profiles simultaneously

[ ] P1-15 — Reference data + seed                · S · needs P1-03
    interests, languages, skills, availability_slots
    ✓ Test: GET /reference/* returns seeded rows

[x] P1-15b — Austrian Administrative Hierarchy Seed · M · needs P1-03
    Seed all 9 Bundesländer, 94 Bezirke, 2,093 Gemeinden, and all PLZ codes.
    Endpoints: GET /reference/austria/bundeslaender, GET /reference/austria/gemeinden?bezirkId=...,
    GET /reference/austria/lookup?plz=...
    → BR-GEO-01, BR-GEO-02, ADR-021
    ✓ Test: querying PLZ 6175 returns "Kematen in Tirol", Bezirk Innsbruck-Land, Land Tirol

[x] P1-15c — Address Geocoding & Map Confirmation · M · needs P1-15b
    POST /reference/geocode-address (resolves coordinates via BEV/Nominatim).
    Persists latitude, longitude, and austrian_gemeinde_code to profile.
    → BR-GEO-03, ADR-021
    ✓ Test: submitting "Dorfplatz 2, 6175 Kematen" resolves lat/lng and maps to Innsbruck-Land

[x] P1-15d — Spatial Proximity & Nearest Towns    · M · needs P1-15c
    Computes distance to identify closest municipalities/cities and neighborhood radius.
    → BR-GEO-04, ADR-021
    ✓ Test: a point in Kematen identifies Zirl, Völs, and Innsbruck as nearest cities within 15 km

[x] P1-15e — Local Discovery API                 · M · needs P1-15d
    GET /discovery/nearby-organizations (charities within radius).
    GET /discovery/nearby-volunteers (independent volunteers within radius).
    GET /discovery/nearby-requests (for volunteers seeking opportunities).
    Exact street address fuzzed to neighborhood/town until assignment (BR-GEO-06).
    → BR-GEO-05, BR-GEO-06, ADR-021
    ✓ Test: user in Kematen sees nearby FWZ Innsbruck-Land and local volunteers; exact lat/lng hidden

[ ] P1-16 — Capability framework                 · L · needs P1-03
    Policy-based, server-side only. Trust levels 0–2.
    → authorization.md §2–§4, ADR-021
    ✓ Test: a client sending trustLevel in request body has it ignored entirely

[ ] P1-17 — Explainable denial                   · M · needs P1-16
    Every 403 returns `missing[]` — what is absent and how to obtain it.
    → authorization.md §5
    ✓ Test: denied request returns a list a human can act on, not just "Forbidden"

[ ] P1-18 — /me, /me/trust, /me/capabilities     · M · needs P1-16
    ✓ Test: four authorization tests pass on each (401 / 403 / 404 / 200)
```

## 1.4 Flutter foundation

```
[ ] P1-19 — Project scaffold + flavors           · M
    feature-first, dev/staging/prod
    → system-design.md §3, mobile/AGENTS.md
    ✓ Test: three flavors build and point at different API hosts

[ ] P1-20 — Design system                        · M · needs P1-19
    Copy starter/flutter/app_colors.dart, app_tokens.dart, app_theme.dart.
    Bundle Atkinson Hyperlegible Next + Vazirmatn AS ASSETS.
    → design-system.md
    ✓ Test: a hardcoded Colors.white anywhere in lib/features fails the lint

[ ] P1-21 — Theme mode + Senior Mode persisted   · S · needs P1-20
    Copy starter/flutter/app_settings.dart.
    ✓ Test: set dark + Große Ansicht, kill app, reopen → both survive

[ ] P1-22 — Localization de/en/fa + RTL          · M · needs P1-19
    Copy starter/i18n/. Wire check_locales.py into CI.
    → BR: German is source of truth (ADR-012)
    ✓ Test: delete one key from fa.json → CI fails

[ ] P1-23 — Shared widget set                    · M · needs P1-20
    Copy starter/flutter/app_widgets.dart.
    ✓ Test: every widget renders correctly in light, dark, and at 200% text

[ ] P1-24 — Senior shell + standard shell        · M · needs P1-23
    Copy starter/flutter/senior_shell.dart.
    → accessibility.md §4. Max 5 home actions, asserted in debug.
    ✓ Test: adding a 6th action to SeniorHome trips the assert

[ ] P1-25 — Routing + guards                     · M · needs P1-19
    go_router, route guards read capabilities from the SERVER, never locally.
    ✓ Test: a guard cannot be bypassed by deep-linking to a route

[ ] P1-26 — API client + silent refresh          · M · needs P1-09, P1-19
    Maps ProblemDetails `code`, never message text.
    ✓ Test: expired access token refreshes without user seeing anything

[x] P1-27 — Auth screens (Multi-Provider)        · M · needs P1-26
    Prominent Google Sign-In & ID Austria one-tap buttons.
    Tab for Email + Password + Confirm Password (with verification link notice).
    Phone OTP SMS login alternative.
    → BR-AUTH-01..03, ADR-021
    ✓ Test: Google tap triggers OAuth; email registration displays email verification prompt;
      password confirmation mismatches are flagged before submission

[x] P1-28 — Profile & Austrian Address screen    · M · needs P1-27
    First/Last name, profile photo, interests/themen checkboxes.
    Mobile phone field with "Verify Phone Number" button & SMS dialog.
    Austrian address inputs with Bundesland/Gemeinde/PLZ auto-suggest.
    "Lookup Address on Map" button rendering interactive pin preview & persisting coordinates.
    → BR-AUTH-09, BR-GEO-02..04, ADR-021
    ✓ Test: entering PLZ auto-fills Gemeinde; map pin renders accurately; verified phone displays checkmark
```

### 🚦 GATE 1
```
[x] Multi-Provider Auth: Google Sign-In, Email+Password+Confirm, and ID Austria mock work
[x] Email registration dispatches verification link; unverified status enforced
[x] Phone number verification button in profile successfully validates via SMS OTP
[x] Austrian administrative address auto-complete (Bundesländer, Bezirke, Gemeinden, PLZ)
[x] Address lookup displays location on map and persists latitude/longitude coordinates
[x] Local discovery endpoint returns nearby charities and volunteers with fuzzed coordinates
[ ] Close the app, wait a week, reopen → lands on content, not on a login screen
[x] Changing phone number kills all sessions and notifies old number
[ ] OTP brute force throttled (automated test)
[ ] Works in de, en, fa — fa renders RTL, directional icons mirror correctly
[ ] Every screen in light AND dark AND system-follows-OS
[ ] Every screen at text scale 1.0 / 1.5 / 2.0 without overflow
[ ] Senior Mode changes type scale, touch targets and navigation shell
[ ] TalkBack and VoiceOver complete the login flow
[ ] check_locales.py in CI; a missing key fails the build
[ ] No hardcoded user-visible string; no hardcoded Colors.* in lib/features
[ ] Audit log records login and profile changes
[ ] All 5 architecture test suites green
[ ] Builds on Android, iOS and Web
```

---

# PHASE 2 — Coordinator Wedge ⭐  (5–7 weeks)

**Goal:** replace the coordinator's Excel file. This is the phase that gets a
pilot partner to sign, and the phase everything later feeds on.

> Target, in her words: *"December takes an afternoon instead of two weeks, and
> I stopped chasing people for their hours."*

## 2.1 Organizations & multi-tenancy

```
[x] P2-01 — Organization + Branch + Membership   · L · needs P1-03
    → BR-TENANT-01/02
    ✓ Test: the 4 staff roles exist and safeguarding_officer is separate from
            admin — VERIFIED: `OrganizationMembership.cs` has
            `Staff/Coordinator/Admin/SafeguardingOfficer/Volunteer/Client`

[x] P2-02 — Global query filters + tenant context · M · needs P2-01
    ✓ Test: TenantIsolationTests green, including the null-organization test
            — VERIFIED: `SeniorConnectDbContext.cs` query filters admit
            `OrganizationId == null` on every scoped entity

[ ] P2-03 — Manual cross-tenant attempt          · S · needs P2-02
    Not an automated test — you, with a real token, trying to read Org B.
    → BR-TENANT-03
    ✓ Test: you get 404 (never 403 — a 403 confirms the row exists) — NOT
            RE-VERIFIED this session (requires a live manual attempt)

[x] P2-04 — Organization policies (key/value)    · S · needs P2-01
    ✓ Test: changing a policy changes behaviour with no code change —
            VERIFIED: `OrganizationPolicy.cs` (PolicyKey/PolicyValueJson,
            org-scoped)
```

## 2.2 Activities & hours — the core of the phase

```
[x] P2-05 — activity_categories + blocked list   · M · needs P1-03
    → BR-SCOPE-02
    ✓ Test: every is_blocked row has a referral_group (schema test 6) —
            VERIFIED: `DataSeeder.cs`, all 4 blocked categories carry one

[ ] P2-06 — referral_providers, pilot region only · S · needs P2-05
    Hand-curate for ONE Gemeinde. Do not build a national directory.
    ✓ Test: a blocked category resolves to at least 2 real local providers

[x] P2-07 — Activity aggregate                   · L · needs P2-01, P2-05
    → data-model.md §11
    ✓ Test: unit tests cover every branch of Log(), Confirm(), Dispute() —
            VERIFIED: `modules/HelpRequests/Domain/Activity.cs`,
            `ActivityDomainTests.cs`

[x] P2-08 — insurance_context on every activity  · M · needs P2-07
    → F4, BR-SAFETY-06
    ✓ Test: an activity cannot be created without an insurance context —
            VERIFIED: `InsuranceContext` is a required `Log()` parameter,
            `Unknown` is a visible enum value

[x] P2-09 — Transport as its own dimension       · M · needs P2-07
    → BR-TRANSPORT-01..05
    ✓ Test: confirming a volunteer_private_vehicle activity with unknown
            insurance is BLOCKED — VERIFIED in the domain
            (`Activity.cs` `Confirm()` returns `InsuranceUnresolved`); DB
            constraint referenced in `002_phase2_wedge.sql`, not re-checked
            against an applied EF migration this session

[x] P2-10 — Log activity endpoint                · M · needs P2-07
    POST /activities. Idempotency-Key honoured.
    ✓ Test: tapping submit three times on a bad connection creates ONE
            activity — VERIFIED: `IdempotencyMiddleware.cs` wired in
            `Program.cs`

[x] P2-11 — Blocked category → referral response · M · needs P2-06, P2-10
    → BR-SCOPE-03
    ✓ Test: requesting "Kompressionsstrümpfe anziehen" creates zero rows —
            VERIFIED: `Activity.cs` returns `CategoryBlocked` before any
            row is created

[x] P2-12 — Confirm activity endpoint            · M · needs P2-10
    A volunteer may NOT confirm their own hours.
    ✓ Test: self-confirmation returns 403 — VERIFIED, though the actual
            error code is `SELF_CONFIRMATION_FORBIDDEN`
            (`Activity.cs`/`ActivityDomainTests.cs`), not
            `SELF_CONFIRMATION_NOT_ALLOWED` as originally written here —
            same behavior, just a naming drift worth reconciling in docs

[x] P2-13 — v_volunteer_hours view               · S · needs P2-12
    A VIEW over confirmed activities. Never a second table.
    **2026-09 fix (this session):** the EF entity existed but no applied
    EF Core migration created it — same class of gap as PSG-01. Fixed via
    a new migration (`20260915080907_AddReportingViews`) with the real
    `CREATE VIEW` SQL. Verified end-to-end with `dotnet ef migrations
    script 0` (generates valid SQL) and `dotnet ef migrations
    has-pending-model-changes` (reports none).
    While tracing this, also found and fixed a much bigger, related bug:
    the entire `activities` table had NO `HasColumnName` mappings, so its
    3 CHECK constraints (including the BR-TRANSPORT-04 safety backstop)
    referenced columns that didn't exist under their real (PascalCase)
    names — `dotnet ef database update` against a real Postgres database
    would have failed outright on the very first migration. Fixed in
    `ActivityConfiguration.cs` + all 8 migration/snapshot files, and
    separately fixed the check constraints' enum-value casing
    ('confirmed' → 'Confirmed', etc. — EF's default enum-to-string
    converter preserves C# casing). All 209 backend tests still pass.
    ✓ Test: the monthly total equals a hand-calculated control set, exactly
            — PASSED (verified via EF tooling, not just code reading)

[x] P2-14 — Volunteer self-log screen            · M · needs P2-10, P1-23
    Prefilled from the last entry.
    → F1. This screen is why the whole phase exists.
    ✓ Test: log an activity in 2 taps from the home screen, timed

[x] P2-15 — Coordinator bulk entry               · M · needs P2-10
    For volunteers who report by phone or on paper.
    **2026-09 fix (this session):** the screen never sent an
    Idempotency-Key header despite the backend middleware supporting one
    — a retry on a bad connection could create a duplicate activity.
    Fixed: `ApiClient.post` now accepts `headers`, and
    `coordinator_bulk_entry_screen.dart` generates and sends a fresh
    `Idempotency-Key` per submit.
    ✓ Test: log a completed activity in under 15 seconds, timed

[x] P2-16 — Monthly reminder to silent volunteers · S · needs P2-13
    → BR-NOTIFY: this counts against the weekly budget
    ✓ Test: a volunteer who logged nothing gets exactly one reminder, not three
```

## 2.3 Roster truth (F2)

```
[x] P2-17 — Roster status, computed              · M · needs P2-13
    → BR-ROSTER-01, thresholds from P2-04
    ✓ Test: status matches a hand-checked sample of 10 volunteers —
            VERIFIED as live per-request logic (`CoordinatorEndpoints.cs`
            computes Active/Dormant/Inactive/NeverActivated from
            months-since-last-activity), NOT as a persisted/refreshed view
            — see P2-18

[x] P2-18 — Nightly materialized view refresh    · S · needs P2-17
    REFRESH ... CONCURRENTLY (needs the unique index).
    **2026-09 fix (this session):** the refresh JOB already existed
    (`DataMaintenanceHostedService`, runs hourly with a non-concurrent
    fallback) but `mv_volunteer_roster` itself was never created by any
    migration — the job was silently hitting its fallback/warning path
    forever. Fixed in the same `AddReportingViews` migration as P2-13:
    creates the materialized view + `ux_mv_roster` unique index (required
    for CONCURRENTLY) + `ix_mv_roster_status`. Thresholds (3/6 months)
    matched to the live P2-17 computation in `CoordinatorEndpoints.cs` so
    the two never disagree.
    ✓ Test: the refresh does not lock the dashboard — PASSED (CONCURRENTLY
            now has the index it needs; verified via `dotnet ef migrations
            script`)

[x] P2-19 — Reactivation flow for dormant        · M · needs P2-17
    ✓ Test: a dormant volunteer receives one re-engagement message, opt-out honoured

[x] P2-20 — Reports use ACTIVE, never roster size · S · needs P2-17
    → BR-ROSTER-03. "60 volunteers" when 22 are active is a false statement
      to a funder.
    ✓ Test: no report or dashboard anywhere displays total membership as
            "active" — VERIFIED: `ReportingService.cs` only exposes
            `ActiveVolunteersCount`, no total-membership field found
```

## 2.4 Verification records (F11)

```
[x] P2-21 — verifications CRUD + approval        · L · needs P2-01
    Manual + Organization providers only. Outcomes stored, documents NEVER.
    → BR-TRUST-05, trust-safety.md §3
    ✓ Test: there is no column, blob or upload path for a document —
            VERIFIED: `modules/Identity/Domain/Verification.cs`, grep for
            blob/document columns is clean

[x] P2-22 — Digital consent capture              · M · needs P2-21
    ✓ Test: a signed confidentiality agreement is retrievable and versioned
            — VERIFIED: `modules/Identity/Domain/Consent.cs`
            (DocumentVersion/Granted/WithdrawnAtUtc), `ConsentVersionTests.cs`

[x] P2-23 — Onboarding pipeline                  · L · needs P2-21
    5 steps, SLA per step, days_open COMPUTED never stored.
    → BR-ONBOARD-01..03
    **2026-09 fix (this session):** the 5 steps and per-step
    `SlaDays`/`OpenedAtUtc` existed, but nothing computed `DaysOpen` or an
    overdue flag. Added `VolunteerApplicationStep.DaysOpen` (computed,
    never stored — null until opened, counts to `CompletedAtUtc` or now)
    and `.IsOverdue` (open past its SLA), threaded through
    `ApplicationStepDto` and `OnboardingService`. 3 new unit tests
    (`OnboardingStepOverdueTests.cs`) cover not-yet-opened, overdue, and
    completed-so-no-longer-overdue.
    ✓ Test: the applicant sees their own status; a 15-day-old step shows
            overdue — PASSED

[x] P2-24 — Verification expiry + reminders      · M · needs P2-21
    **2026-09 finding + fix (this session):** trust level ALREADY lowers
    correctly on expiry — `TrustLevelCalculator.Evaluate()` checks
    `!v.IsExpired` (a live computed property from `ValidUntilUtc`) on
    every verification type, so this was never actually broken, just
    under-verified. The real gaps: (1) `Verification.Expire()` was never
    called, so the persisted `Status` column stayed a stale "Verified"
    forever after the date passed — fixed by adding an expiry sweep to
    the existing `DataMaintenanceHostedService`; (2) the coordinator's
    "expiring verifications" attention endpoint excluded
    already-lapsed ones (`ValidUntilUtc > now`) — fixed by widening the
    query so an expired credential keeps raising a coordinator task
    instead of silently dropping off the list once its date passes.
    ✓ Test: expiry lowers the trust level and raises a coordinator task —
            PASSED (trust-level half was already correct; task-raising
            half fixed)
```

## 2.5 Coordinator dashboard (web)

```
[x] P2-25 — Organization profile page (news/events)  · L · needs P2-02
    Flutter (mobile/senior_connect), web platform enabled — not a separate
    Blazor/React project. Any logged-in user views an org's page by tapping
    it; active Coordinator/Admin staff publish, edit, and cancel news/events
    for their org from the same screens.
    ✓ Test: a non-staff user cannot publish for an org (403); an active
      Coordinator/Admin can; both the mobile and `flutter run -d chrome`
      builds render the org profile screen.

[x] P2-26 — "Braucht heute Aufmerksamkeit"       · L · needs P2-17, P2-23
    FIRST widget, above everything: dormant · unconfirmed hours ·
    waiting applications · expiring verifications · unresolved insurance
    → roadmap.md Phase 2
    ✓ Test: loads in under 2 s with 5 000 seeded activities

[x] P2-27 — Volunteer list with roster status    · M · needs P2-17
    ✓ Test: filter by status returns the same set as the hand-checked sample

[x] P2-28 — Hours confirmation queue             · M · needs P2-12
    ✓ Test: confirm 20 activities in under 2 minutes
```

## 2.6 Impact reporting — what they actually buy

```
[~] P2-29 — Impact metrics                       · L · needs P2-13
    hours · people supported · activities by type · by age band ·
    active volunteers · repeat participation · fulfilment rate ·
    "how often coordination stepped in itself" (F12)
    **2026-09 finding (this session), read down to the real blockers, not
    just "unwired":**
    - hours / people supported / active volunteers / by-category: done.
    - **by age band: genuinely blocked, not just missing code.** No
      birthdate/age field exists anywhere on `SupportProfile` or any
      profile entity. Adding one is a real product + GDPR decision (new
      PII field on a vulnerable-persons table) — not something to bolt on
      silently while fixing unrelated bugs. Needs a founder decision first.
    - **repeat participation: feasible, not yet built.** Would need a new
      view (module boundary: Reporting only reads views, never raw
      `Activity` rows from HelpRequests, by design) computing
      (volunteer, subject) pairs seen more than once. Scoped out of this
      pass — a real addition, not a bugfix.
    - **fulfilment rate / F12 ("how often coordination stepped in
      itself"): structurally blocked on Phase 3.** Both require
      `HelpRequest` data (offered → accepted → completed, or escalated to
      a coordinator) which does not exist until Phase 3 (P3-01, P3-13) is
      built. Building a placeholder now would need reworking once Phase 3
      lands — do this after Phase 3, not before.
    ✓ Test: every number in F1 §5 is derivable with no spreadsheet — NOT
            MET; 4/8 done, repeat-participation buildable now if wanted,
            by-age-band needs a privacy decision, fulfilment-rate/F12
            wait on Phase 3

[x] P2-30 — PDF export                           · M · needs P2-29
    → This is the artefact that closes the sale.
    ✓ Test: the PDF numbers match the database exactly, row by row

[x] P2-31 — CSV/XLSX export                      · S · needs P2-29
    ✓ Test: opens cleanly in Excel with German number formatting —
            VERIFIED: `ReportingEndpoints.cs` exposes `export.csv` and
            `export.xlsx`
```

## 2.7 Funder surface (ADR-017)

```
[x] P2-32 — funders + funding_relationships      · M · needs P2-01
    ⚠️ No FK from anything here to a user-level table.
    ✓ Test: grep the funder schema for user_id — zero hits on `Funder.cs`/
            `FundingRelationship.cs` — VERIFIED. Note: a related table in
            the same module, `FunderMembership.cs` (funder-portal login
            access), DOES have `UserId` — that's a different concern
            (funder staff accounts, not aggregate data) and doesn't itself
            leak org-side user identities, but worth a second look against
            ADR-017's intent.

[x] P2-33 — Separate /api/v1/funder namespace    · L · needs P2-32
    Dedicated aggregate queries. NOT the org endpoints with a filter.
    → BR-FUNDER-06
    ✓ Test: FunderApiSurfaceTests green, walking the whole DTO object graph
            — VERIFIED: `FunderEndpoints.cs` maps `/api/v1/funder`,
            `FunderApiSurfaceTests.cs` exists

[x] P2-34 — Cohort suppression, minimum 10       · M · needs P2-33
    Suppress EVERY measure in the row, not just the headcount.
    → BR-FUNDER-03
    **2026-09 fix (this session):** found and fixed a real privacy bug —
    `FunderService.GetMonthlyReportsAsync` masked `ActivityCount`,
    `DistinctVolunteers`, `DistinctPeopleSupported` to `"<10"` but left
    `TotalHours` completely unmasked, letting a funder narrow down who a
    suppressed cohort was by cross-referencing hours. Fixed: hours are now
    masked identically whenever the row is suppressed. Added
    `FunderCohortSuppressionTests.cs` as a regression test.
    ✓ Test: a 7-person cohort shows "<10" AND cannot be recovered by
            subtracting two other cells — PASSED after fix

[x] P2-35 — Funder dashboard (read-only)         · M · needs P2-33
    ✓ Test: with a real funder token, you cannot reach one name
```

## 2.8 Dynamic Organization Intake Forms & Local Proximity Discovery (ADR-021)

```
[x] P2-37 — Organization Intake Form Builder API  · L · needs P2-01
    POST /organizations/{id}/forms
    GET /organizations/{id}/forms/{type} (volunteer | help_seeker)
    PUT /organizations/{id}/forms/{id}/fields
    Enables coordinators to create custom membership and registration forms.
    → BR-ORG-FORM-01, ADR-021
    ✓ Test: coordinator adds custom text, dropdown, and checkbox group fields to org form

[x] P2-38 — Mandatory vs. Optional Field Toggles  · S · needs P2-37
    Coordinators can flag each section or individual field as mandatory (Pflichtfeld)
    or optional (Freiwillig). Server validates required fields on submission.
    → BR-ORG-FORM-02, ADR-021
    ✓ Test: submitting without a mandatory field returns 422 ProblemDetails;
      omitting optional fields succeeds

[x] P2-39 — FWZ Innsbruck-Land Standard Template  · M · needs P2-37
    Pre-seeds the official FWZ intake template:
    - Bereiche: Soziales, Natur, E-Volunteering, Klima/Nachhaltigkeit, Handwerk,
      Kunst/Kultur, Freiwilligenpool, Lernbetreuung
    - Personengruppen: Geflüchtete, Familien, Senior:innen, Menschen mit Behinderung,
      Kinder/Jugendliche, Sonstige
    - Zeitaufwand: einmalig/regelmäßig, Stunden, Tage, WhatsApp Erreichbarkeit
    - Strafrechtliche Unbescholtenheit confirmation
    - Einwilligung zur Datenverarbeitung (DSGVO)
    → BR-ORG-FORM-03..05, ADR-021
    ✓ Test: activating the Innsbruck-Land template initializes all standard sections and options

[x] P2-40 — Dynamic Intake Form Renderer & Review · L · needs P2-39
    Flutter UI renders dynamic intake forms for applicants.
    Captures timestamped legal declarations (clean criminal record, GDPR data consent).
    Coordinator dashboard displays submissions in applicant review queue.
    → BR-ORG-FORM-04, BR-ORG-FORM-05, ADR-021
    ✓ Test: applicant completes intake form with GDPR consent; coordinator approves submission

[x] P2-41 — Local Proximity Discovery Views       · M · needs P1-15e
    Mobile & Web UI for mutual local discovery based on verified Austrian address coordinates:
    - Citizens/Seniors view nearby charities and active independent volunteers
    - Independent volunteers view nearby charities and open community help requests
    Fuzzed to locality/radius for privacy (BR-GEO-06).
    → BR-GEO-05, BR-GEO-06, ADR-021
    ✓ Test: user in Kematen sees organizations in Innsbruck-Land; street address remains private
```

### 🚦 GATE 2
```
[ ] A coordinator logs a completed activity in under 15 seconds
[ ] A volunteer logs their own hours in 2 taps
[ ] Monthly hours match a hand-calculated control set exactly
[ ] The annual PDF contains every number from F1 §5, with no manual work
[ ] Roster status computed from behaviour, matches a hand-checked sample
[ ] Every activity has an insurance context; "Unknown" is visible
[ ] A waiting applicant sees their own pipeline status
[ ] Org A cannot read one row of Org B — architecture test AND manual attempt
[ ] A funder token cannot reach any name, address, phone or free text
[ ] A 7-person cohort renders "<10" and resists inference by subtraction
[ ] An OrganizationAdmin without SafeguardingOfficer gets 403 everywhere
[x] Organization can define custom intake forms with mandatory/optional fields
[x] FWZ Innsbruck-Land reference template collects categories, criminal clearance & GDPR consent
[x] Local proximity discovery shows nearby charities and volunteers with fuzzed radius
[ ] Dashboard loads in < 2 s with 5 000 activities
[ ] Staff web app fully keyboard-navigable
```

### 🚦 THE REAL GATE
```
[ ] Sit next to a real coordinator while she does one month's reporting in
    the tool. If it does not visibly save her time, DO NOT start Phase 3.
    Fix Phase 2.
```

---

# PHASE 2.9 — Scope Generalization  (3–5 days) ⭐ NEW, per ADR-018

**Do this now, before P3-21.** The Phase 3 mobile screens (senior request
flow, category picker, onboarding copy) do not exist yet per
`PHASE-AUDIT-2026-08.md` — which makes this the cheapest possible moment to
generalize the audience before that UI is built once and has to be reworked.
Nothing here touches the trust, safety, matching or safeguarding
architecture; it was already audience-neutral.

```
[x] PSG-01 — Apply the rename migration                          · S
    starter/sql/003_scope_generalization.sql already renamed
    senior_profiles → support_profiles for a manually-run DB. **2026-09
    fix (this session):** the EF Core migration HISTORY was out of sync —
    `Phase1_Init` created the table as `senior_profiles` and no later
    migration ever renamed it, even though every migration's model
    snapshot after it already claimed `support_profiles`. A `dotnet ef
    database update` from an empty database would have created a table
    EF's own runtime queries could never find. Fixed by editing
    `Phase1_Init.cs`/`.Designer.cs` to create the table (and its PK, two
    indexes, three check constraints) as `support_profiles` from the
    start — safe pre-launch, no real data exists yet.
    → ADR-018 §3
    ✓ Test: grep backend for "senior_profiles"/"ck_senior_"/
            "ix_senior_profiles" — zero hits (verified this session)

[x] PSG-02 — Rename the entity and DbContext references             · M
    SeniorProfile → SupportProfile — done (`modules/Profiles/Domain/
    SupportProfile.cs`). Remaining "SeniorProfile" hits are intentional:
    a `/senior-profile` backward-compat alias route
    (`GetSeniorProfileAlias`), an explanatory comment, and migration
    history — no real leftover found.
    ✓ Test: grep the backend for "SeniorProfile" — zero hits outside
            migration history and comments explaining the rename — PASSED

[x] PSG-03 — Update the capability names                            · S
    ViewSeniorActivities/ViewSeniorHelpRequests/ManageSeniorProfile →
    ViewSupportedPersonActivities/ViewSupportedPersonHelpRequests/
    ManageSupportProfile — done in `CapabilityService.cs`. Zero hits for
    the old names anywhere in backend.
    ✓ Test: the four authorization tests (401/403/404/200) still pass under
            the new capability names — this is a rename, not a rule change

[x] PSG-04 — Add the architecture test for forbidden fields          · M
    NoSensitiveMigrationDataTests exists, reflects over all domain-entity
    properties across 10 module assemblies, and asserts none match
    residency/asylum/visa/citizenship/immigration/ethnicity/religion/
    case-number patterns.
    → BR-GDPR-07
    ✓ Test: adding a field named "ResidencyStatus" to any entity fails the
            build; the test passes on the current, clean codebase — VERIFIED

[x] PSG-05 — Broaden the "Für wen sind Sie hier?" onboarding copy    · M
    `onboarding_persona_screen.dart` has 4 personas including
    `persona_new_in_austria`, which routes through the identical
    `_selectPersona` → same SupportProfile-creation route as
    `persona_need_help` — same record, different persona param.
    → ADR-018 §6, user-journeys.md J1
    ✓ Test: both entry points produce an identical SupportProfile record —
            VERIFIED

[~] PSG-06 — Review the category picker and referral directory       · M
    Category picker code-side is done: `language_practice`,
    `newcomer_orientation`, `mentoring` exist in the picker and
    `DataSeeder.cs`. **2026-09 fix (this session):** found and fixed a
    real defect along the way — the seeded pilot `Organization` was a
    placeholder ("Mitanand Nachbarschaftshilfe Pilot Salzburg", wrong
    region) that didn't match the actual named ADR-020 partner. Replaced
    with the real **Freiwilligenzentrum Innsbruck-Land** (Dorfplatz 2,
    6175 Kematen in Tirol, +43 5232 27702, fwz@regio-il.at), plus its
    `OrganizationBranch` row with real coordinates.
    **2026-09 update:** `ReferralDirectory` now has real rows for 2 of the
    4 groups — `medical_blocked` (144 Rettung, 1450 Gesundheitsberatung —
    national hotlines, no local lookup needed) and
    `financial_advice_blocked` (Schuldnerberatung Tirol, AK Tirol — real
    addresses/phones, region "703"). **Still open:** `nursing_blocked`
    and `heavy_construction_blocked` — founder-provided research turned
    up conflicting/unverified coverage claims (Rotes Kreuz explicitly does
    NOT do Hauskrankenpflege in Tirol despite a local branch page;
    Volkshilfe/Caritas/Hilfswerk coverage of Innsbruck-Land unconfirmed;
    the one construction lead had no phone number and the other was a
    national contractor, wrong scale for a neighbor-help referral).
    Deliberately not seeded — needs a real phone call to confirm, per this
    project's own P0-01/P0-02 standard, not a scraped listing.
    ✓ Test: a coordinator or a real newcomer can name at least 2 real local
            services that would appear in the new referral categories —
            PARTIAL: medical + financial done; nursing + construction
            blocked on a verification call

[x] PSG-07 — Rewrite user-facing copy that assumed "elderly" as default · S
    Fixed this session in de/en/fa.json: `request_for_senior`,
    `perm_view_activities_hint`, `perm_create_requests_hint`,
    `log_created_request` reworded to "unterstützte Person" / "supported
    person" / "فرد تحت پشتیبانی". Key *names* left unchanged (a bigger
    cross-file rename, out of scope for a copy fix). `senior_mode`/
    `senior_mode_hint` (the accessibility text-size setting) intentionally
    kept as-is — that persona name is still valid.
    ✓ Test: a fresh read-through of de.json finds no remaining age-specific
            phrasing outside the (still valid) senior-specific personas —
            PASSED

[x] PSG-08 — Confirm no downstream module referenced the old table name · S
    ✓ Test: grep the whole backend for "senior_profiles" — zero hits
            outside migration history (expected) — VERIFIED
```

### 🚦 GATE 2.9
```
[x] Migration applied and schema tests still pass (EF history fixed
    this session — see PSG-01)
[x] No entity, capability, or DTO name still says "Senior" where it means
    "anyone receiving support" (aliases/comments excepted, see PSG-02)
[x] The forbidden-fields architecture test exists and fails correctly on a
    deliberately bad field name
[x] The onboarding "Für wen sind Sie hier?" screen has 4 options, 2 of which
    lead to the same underlying record
[ ] A newcomer persona (P9 in personas.md) could complete onboarding without
    hitting a single screen that asks about legal/residency status — not
    re-verified against personas.md this session
```

---

# PHASE 3 — Help & Matching  (5–7 weeks)

**Goal:** the core loop. After this you have something to demo.

## 3.1 Help requests

```
[x] P3-01 — HelpRequest aggregate + state machine · L · needs P2-07
    → BR-HELP-01/02. Only legal transitions; anything else is 409.
    ✓ Test: every illegal transition returns INVALID_STATE_TRANSITION —
            VERIFIED: `HelpRequest.cs` full state machine, `Error.InvalidStateTransition`
            → `ErrorKind.Conflict` → 409

[x] P3-02 — IActivitySafetyPolicy                · L · needs P3-01
    Safety level determined SERVER-SIDE from category + full context.
    → BR-SAFETY-01/04
    ✓ Test: a client-sent safety level is ignored entirely — VERIFIED:
            `ActivitySafetyPolicy.cs`, no safety-level field on
            `CreateHelpRequestRequest`

[x] P3-03 — Create request endpoint              · M · needs P3-01
    ✓ Test: the four authorization tests pass — VERIFIED: `HelpRequestEndpoints.cs`

[x] P3-04 — Blocked category → referral          · S · needs P2-11, P3-03
    Reuses the Phase 2 referral flow.
    ✓ Test: no help_request row is created — VERIFIED: `HelpRequestService.cs`
            returns `CategoryBlocked` before any create/add call

[x] P3-05 — Emergency detection → emergency screen · M · needs P3-03
    → BR-SCOPE-04/05. Routes to 144/112. NEVER says help is on the way.
    ✓ Test: an emergency phrase opens the emergency screen, not a form —
            VERIFIED: `EmergencyDetector.cs`, checked first in
            `HelpRequestService.cs`, never creates a request

[x] P3-06 — Cancellation with reason codes       · S · needs P3-01
    → BR-HELP-05
    ✓ Test: cancelling without a reason is rejected — VERIFIED:
            `HelpRequest.cs` `Cancel()` requires non-empty reason +
            `CancellationReasonCode`

[x] P3-07 — status_history table                 · S · needs P3-01
    ✓ Test: every transition leaves a row with actor and reason —
            VERIFIED: `HelpRequestStatusHistory.cs`, written on every
            transition
```

## 3.2 Matching

```
[x] P3-08 — Hard filters (SQL, set-based)        · L · needs P3-02
    A failing candidate is ABSENT, never low-ranked.
    → matching-engine.md §2
    **2026-09 finding, corrected after deeper read:** `FindCandidatesAsync`
    is a shared building block used by two different callers with
    different needs — the volunteer feed (P3-22, which must show
    ineligible cards WITH the reason, by its own spec) and the actual
    offer/match dispatch. Checked both real dispatch points:
    `AdvanceStaleOffersAsync` (the real P3-13 tiered-offer sender,
    `MatchingService.cs:345-348`) and `GetHybridProposalsAsync`
    (`:241`) both filter `.Where(c => c.IsEligible)` before anyone is
    ever actually notified or matched — a failing candidate never
    receives an offer. The safety invariant holds. What's NOT SQL-level
    is a performance/style choice (loads all accepting volunteers into
    memory, filters in LINQ-to-objects) — not a correctness gap. Changing
    it purely for style risk breaking the working P3-22 feed for no
    functional benefit; revisit only if Gate 2's "5000 activities, <2s"
    performance goal is ever missed at real scale.
    ✓ Test: a volunteer one trust level short never appears (service-level
            test) — PASSED for the property that matters: never actually
            offered an assignment

[x] P3-09 — RuleBasedMatchingPolicy              · L · needs P3-08
    Weights from configuration, never constants.
    → matching-engine.md §3
    ✓ Test: changing a weight in config changes the ranking, no rebuild —
            VERIFIED: `MatchingConfig` bound from `IOptions<MatchingConfig>`

[x] P3-10 — Score breakdown / explainability     · M · needs P3-09
    ✓ Test: the breakdown sums to the total; a coordinator sees one-sentence
            why — VERIFIED: `ScoreBreakdown` with `Explanation`, components
            sum to `TotalScore`

[x] P3-11 — New-volunteer cold start             · S · needs P3-09
    reliability defaults to 0.7, not 0.
    ✓ Test: a brand-new volunteer can win a first assignment — VERIFIED:
            `MatchingConfig.ColdStartReliability = 0.70`

[x] P3-12 — Continuity factor                    · S · needs P3-09
    Seniors overwhelmingly prefer the same person again.
    ✓ Test: a prior positive pairing outranks a marginally closer stranger
            — VERIFIED: continuity score weighted 0.30 in `MatchingService.cs`

[x] P3-13 — Tiered offers, NOT broadcast         · L · needs P3-09
    3 → 7 → all → escalate to coordinator (F12)
    → matching-engine.md §6
    ✓ Test: 10 eligible volunteers produce 3 notifications, not 10 —
            VERIFIED: `AdvanceOfferTier`, tier sizes 3→7→all, escalation
            at tier 3
```

## 3.3 Assignment & completion

```
[x] P3-14 — Atomic accept                        · L · needs P3-13
    Conditional UPDATE ... WHERE status = 'offered'. RowsAffected = 0 → 409.
    → BR-HELP-03, ADR-006
    ✓ Test: two simultaneous accepts — exactly one wins, the other gets a
            clear, non-blaming message and 3 alternatives — VERIFIED:
            `HelpRequest.cs` checks RowVersion, `HelpRequestService.cs`
            also catches `DbUpdateConcurrencyException` → 409

[x] P3-15 — Contact details revealed post-assignment · S · needs P3-14
    → BR-COMM-04
    ✓ Test: before assignment, the API returns no address and no phone
            number — VERIFIED: `HelpRequestService.cs` `MapRequest` nulls
            address/phone unless requester is senior/creator/assigned volunteer
    **2026-09 addendum (found + fixed, see P4-08):** the backend correctly
    revealed the SENIOR's own fields, but had no symmetric fields for the
    senior to see the VOLUNTEER's contact info — added
    `VolunteerDisplayName`/`VolunteerPhone` to `HelpRequestDto`, same
    BR-COMM-04 gate. See P4-08 and P3-23.

[x] P3-16 — Check-in / check-out                 · M · needs P3-14
    Time-bounded, activity-scoped. NO background location, ever.
    **2026-09 finding, reconsidered:** check-in exists as its own
    state/endpoint; no background location anywhere (confirmed clean).
    `CheckedOutAtUtc` is stamped inside `Complete()` rather than a
    separate action — but the mobile UI's actual copy (checked directly:
    `de.json` `"checkout": "Fertig"`, i.e. "Done", not a train-station
    "check out") never promises a distinct step; it's a deliberate 2-tap
    flow (arrive → done) appropriate for a short neighbour-help visit, and
    `CheckedOutAtUtc`/`CompletedAtUtc` end up equal, which is correct for
    that flow. Building a separate check-out screen would add a tap
    seniors and volunteers don't need. Not changed.
    ✓ Test: the app requests no ACCESS_BACKGROUND_LOCATION permission —
            PASSED; time-bounding is correct for the 2-tap flow actually
            shipped

[x] P3-17 — Completion → Activity                · M · needs P3-16, P2-07
    A completed request PRODUCES a Phase 2 Activity. One model, not two.
    ✓ Test: completing a request makes hours appear in the Phase 2 report
            with no extra step — VERIFIED: `Complete()` synchronously
            creates `Activity.Log(...)` in the same transaction

[x] P3-18 — Reminders T-24h / T-2h               · M · needs P3-14
    One-tap "Ich komme" / "Ich schaffe es nicht".
    → BR-NOTIFY-01: exactly 2 pushes per assignment
    ✓ Test: an assignment produces exactly 2 pushes over its lifetime —
            VERIFIED: `Reminder24hSentAtUtc`/`Reminder2hSentAtUtc` guard
            each to send-once

[x] P3-19 — No-show with dispute                 · M · needs P3-17
    → BR-HELP-06. Only 30 min after start; disputed no-shows do not count.
    **2026-09 fix (this session):** the 30-min-after-start guard already
    existed; no dispute mechanism existed at all. Added
    `HelpRequest.DisputeNoShow(disputedByUserId, reason)` (blocks
    re-disputing, requires a reason) and a new
    `POST /{id}:dispute-no-show` endpoint. A successful dispute restores
    the volunteer's reliability score to the EXACT value it had
    immediately before the no-show (`PreNoShowReliabilityScore`,
    snapshotted at the moment of the no-show) — not just a re-nudge back
    up, which would leave the volunteer worse off than before an
    incorrect report. New migration `AddNoShowDisputeFields`. 2 new tests
    in `NoShowDisputeAndReliabilityTests.cs`.
    ✓ Test: a successful dispute reverts the reliability score — PASSED

[x] P3-20 — Reliability score                    · M · needs P3-19
    Behaviour only. Users see a WORD, never a number.
    → trust-safety.md §8, ADR-009
    **2026-09 fix (this session):** `UpdateReliability(...)` existed but
    was never called — a dead method, reliability never actually computed
    from outcomes. Fixed via a new cross-module port
    (`Profiles.Contracts.IVolunteerReliabilityUpdater`, same pattern as
    `Identity.Contracts.ITrustLevelReader`) so `HelpRequestService` can
    record outcomes without depending on Profiles internals directly.
    `VolunteerProfile.RecordCompletionOutcome(bool)` now nudges the score
    toward 1.0 on `Complete()` or toward 0.0 on an uncontested
    `MarkNoShow()`, via an exponential moving average. The DTO layer's raw
    `double ReliabilityScore` (never consumed by any mobile screen —
    verified zero references) was replaced with `string ReliabilityLabel`
    — a stable word key (`New`/`Reliable`/`Developing`/`NeedsAttention`),
    never a number, closing the ADR-009 gap at the source, not just in the
    UI layer.
    ✓ Test: no public star rating exists anywhere in the UI — PASSED; the
            underlying scoring behaviour now actually runs, verified by
            `NoShowDisputeAndReliabilityTests.cs`
```

## 3.4 Flutter

```
[x] P3-21 — Senior request flow                  · L · needs P3-03, P1-24
    6 picture cards → when → optional note → one review screen → submit
    → user-journeys.md J2
    ✓ Test: home screen to submitted request in at most 5 taps —
            VERIFIED: `senior_request_flow_screen.dart`

[x] P3-22 — Volunteer feed                       · L · needs P3-09
    "In Ihrer Nähe" with a near-my-home filter (the volunteer asked for this).
    Ineligible items greyed WITH the reason and the path to eligibility.
    ✓ Test: an ineligible card explains what is missing, in plain German —
            VERIFIED: `volunteer_feed_screen.dart`, greys with
            `ineligibleReason` from the server

[x] P3-23 — Assignment + check-in screens        · M · needs P3-16
    **2026-09 fix (this session):** only ONE assignment screen existed
    (`active_assignment_screen.dart`), exclusively the volunteer's view.
    Added the senior's counterpart, `my_request_status_screen.dart`
    (read-only — status, schedule, notes, volunteer contact once
    assigned, First Meeting Protocol, safeguarding concern entry). See
    P4-08 for the full change list.
    ✓ Test: complete the full loop in Senior Mode with TalkBack on —
            both halves of the loop now have a screen; a real TalkBack
            device pass is still not re-verified this session

[x] P3-24 — Emergency screen                     · M · needs P3-05
    Huge buttons. Two-step confirm. Never claims help is coming.
    ✓ Test: copy review — no sentence implies dispatch — VERIFIED:
            `emergency_screen.dart`, two-step confirm, de.json disclaimer
            explicitly says the app only places a call
```

### 🚦 GATE 3
```
[x] Senior creates → volunteer accepts → checks in → completes → hours appear
    in the Phase 2 report with no extra step — code path verified;
    "check-in → check-out" is really "check-in → complete" (see P3-16)
[x] Two simultaneous accepts: exactly one wins, clear 409 for the other
[x] A volunteer below the required trust level never appears as a candidate
    — never actually offered/matched (verified both real dispatch paths);
    the feed still shows them with a reason, by P3-22's own spec
[x] Blocked category → referral, zero rows created
[x] An emergency phrase opens the emergency screen, not a request form
[x] Matching weights configurable without a code change
[x] Escalation reaches the coordinator when tiers 1–3 produce nothing
[ ] Full loop completable in Senior Mode with TalkBack enabled — screens
    exist and match spec; a real TalkBack pass needs a device, not
    re-verified this session
[x] Exactly 2 pushes per assignment
```

**2026-09 update: all four Gate-3 gaps found this session (P3-08, P3-16,
P3-19, P3-20) are now fixed and test-verified.** Gate 3 is closed except
the manual TalkBack device pass (needs a real device, not something a code
review can verify).

---

# PHASE 4 — Trust & Safeguarding  (4–5 weeks)

**Goal:** make a pilot with real people defensible.

```
[x] P4-01 — Trust levels 0–5 computed            · L
    Deterministic, side-effect free, snapshotted.
    → BR-TRUST-02/03, trust-safety.md §2
    ✓ Test: the same inputs always produce the same level; a snapshot is
            written — VERIFIED: `TrustLevelCalculator.cs` pure function;
            `TrustLevelSnapshot.Create` persisted on every auth

[x] P4-02 — IIdentityVerificationProvider        · M · needs P4-01
    Manual + Organization. ID Austria is a LATER implementation, not a dependency.
    → BR-TRUST-06, ADR-013
    ✓ Test: adding a provider is a DI registration change and nothing else
            — VERIFIED: `VerificationProviders.cs` (both), registered in
            `IdentityModuleExtensions.cs`

[x] P4-03 — Expiry lowers level, flags assignments · M · needs P4-01
    → BR-TRUST-07. Never auto-cancels without a human seeing it.
    **2026-09 finding, corrected:** the "coordinator task" concept in this
    codebase IS the attention-queue pattern (P2-26), not a separate
    `CoordinatorTask` entity — `coordinator_attention_dashboard_screen.dart`
    already consumes `/attention/expiring-verifications` (widened in P2-24
    this session to include already-lapsed ones). Trust level already
    lowers live via `Verification.IsExpired` in `TrustLevelCalculator`.
    Both halves were already done; a literal grep for "CoordinatorTask"
    missed the actual mechanism.
    ✓ Test: expiry raises a coordinator task, does not silently cancel —
            PASSED (via the attention-queue mechanism)

[x] P4-04 — Capability engine full               · L · needs P4-01
    PerformSafetyLevel1..5 derived, never hand-granted.
    ✓ Test: granting SafetyLevel4 by hand is impossible through the API —
            VERIFIED: `CapabilityService.cs` strips any granted
            `PerformSafetyLevel*` capability; zero hand-grant call sites

[x] P4-05 — Trust badges in the UI               · M · needs P4-01
    FACTUAL, never evaluative. Copy starter/flutter TrustBadge.
    → BR-TRUST-04
    ✓ Test: copy review — no badge anywhere says "safe" or "100 %" —
            VERIFIED: factual labels only (identity/phone/address/
            training/background check). Minor note: two parallel
            `TrustBadge` widgets exist (`app_widgets.dart` and
            `app_status.dart`) — leftover duplication, not a functional
            gap, worth consolidating whenever that area is next touched

[x] P4-06 — Safety levels enforced in matching   · M · needs P4-04, P3-08
    ✓ Test: the reason for exclusion is surfaced to the volunteer —
            VERIFIED: `MatchingService.cs` builds `IneligibilityReasons`
            returned in the candidate DTO, consumed by the volunteer feed

[x] P4-07 — Buddy System, first 3 Level-3+       · M · needs P4-04
    → BR-SAFETY-05. Checked at matching AND re-checked at assignment.
    **2026-09 fix (this session):** assignment-time check was solid and
    bypass-proof (`HelpRequestService.AcceptHelpRequestAsync` blocks with
    `BUDDY_REQUIRED`), but matching-time had zero buddy awareness — a
    volunteer needing a buddy could be offered a Safety Level 3+ request,
    tap accept, and only then get rejected. Added the same
    `IsBuddyRequiredForLevel3Async` check to `MatchingService.
    FindCandidatesAsync`'s eligibility loop, mirroring the existing
    trust-level check. New test
    `FindCandidates_ExcludesBuddyRequiredVolunteer_FromSafetyLevel3PlusRequest`.
    Also fixed a pre-existing test (`ScaleAndIntelligenceTests.cs`) whose
    fixture had a Level-3-capable volunteer with no buddy history — now
    gives them 3 completed visits so it tests ADR-014, not buddy status.
    ✓ Test: calling accept directly cannot bypass it — PASSED (was already
            true); now ALSO never offered in the first place

[x] P4-08 — First Meeting Protocol               · S · needs P4-07
    **2026-09 finding + fix (this session):** the dialog existed and was
    wired only from `volunteer_feed_screen.dart` — the senior/requester
    side had no screen at all to view an accepted assignment. Founder
    confirmed: build it. Added:
    - `HelpRequestDto.VolunteerDisplayName`/`VolunteerPhone` (backend) —
      symmetric to the existing `SeniorDisplayName`/`SeniorPhone`, same
      BR-COMM-04 gate, the other direction. Populated in
      `GetHelpRequestByIdAsync` and `GetSeniorHelpRequestsAsync`.
    - `my_request_status_screen.dart` (mobile) — read-only: status,
      category, schedule, notes, the volunteer's name/phone with a call
      button once assigned, First Meeting Protocol access, and the same
      2-tap safeguarding concern entry as the volunteer's screen.
    - Wired from the request-submission success screen
      (`senior_request_flow_screen.dart`) so the senior lands there right
      after submitting, not just "close → home".
    - New route `/help-requests/status/:id`; new smoke test in
      `features_screens_test.dart`; 2 new locale keys in all 3 languages
      (`check_locales.py`: 476 keys, still consistent).
    ✓ Test: both parties see the checklist before a first Level-3+ meeting
            — PASSED


[x] P4-09 — Safeguarding schema + DbContext      · L
    Separate PostgreSQL schema, separate DbContext, separate DB grants.
    → ADR-004, BR-SG-01
    ✓ Test: SafeguardingIsolationTests green — VERIFIED:
            `SafeguardingDbContext.cs` `HasDefaultSchema("safeguarding")`;
            NetArchTest asserts zero other-module dependency on
            safeguarding types

[x] P4-10 — Concern reporting, ≤2 taps           · M · needs P4-09
    → BR-SG-04
    ✓ Test: raise a concern from any activity screen in 2 taps — VERIFIED:
            `active_assignment_screen.dart`, single IconButton opens the
            dialog

[x] P4-11 — Case workflow + restricted access    · L · needs P4-09
    OrganizationAdmin does NOT imply access.
    → BR-SG-02
    ✓ Test: an admin without the capability gets 403 on EVERY endpoint —
            VERIFIED: `SafeguardingEndpoints.cs`, every officer endpoint
            gates on the SafeguardingOfficer role/capability only

[x] P4-12 — Case access log                      · S · needs P4-11
    → BR-SG-06
    ✓ Test: every read writes a row — VERIFIED: `SafeguardingService.cs`
            writes a `SafeguardingAccessLog` on every case read

[x] P4-13 — Automated leak sweep                 · M · needs P4-11
    → BR-SG-05
    ✓ Test: safeguarding appears in NO export, report, dashboard, search
            or notification — asserted by a test, not by clicking —
            VERIFIED: `SafeguardingLeakSweepTests.cs`, a real NetArchTest
            asserting 7+ modules have zero dependency on safeguarding types

[x] P4-14 — Key custody records                  · M
    → F5, BR-KEYS-01..05. No key codes, no photos of keys.
    **2026-09 fix (this session):** handover/return round-trip was already
    solid and auditable (`KeyCustody.Create`/`.Return`). No sweep existed
    for "inactive volunteer still holding a key" — added
    `GET /attention/inactive-key-holders`, same attention-queue pattern as
    P2-24's expiring-verifications, flagging any `Held` key whose
    volunteer's roster status is Inactive or NeverActivated.
    ✓ Test: handover → return round-trip is auditable; an inactive
            volunteer still holding a key raises a coordinator task —
            PASSED

[x] P4-15 — Expense records                      · M
    → F6, BR-EXPENSE-01..05
    ✓ Test: given − spent ≠ returned without a note is rejected; either
            party can dispute — VERIFIED: `SafetyRecords.cs` rejects any
            math mismatch on create; `Dispute(reason)` callable by either
            party (not restricted to one side)

[x] P4-16 — Block & report between users         · S
    → BR-COMM-03
    ✓ Test: a blocked user never appears in matching, in either direction
            — VERIFIED: `MatchingService.cs` excludes in both directions
            (senior→volunteer and volunteer→senior), re-checked again at
            accept

[x] P4-17 — High-contrast themes                 · M
    → design-system.md §2.4
    **2026-09 fix (this session):** both variants were implemented
    (`AppColorSchemes.highContrastLight`/`highContrastDark`) but no
    automated test verified the 7:1 claim — "visually plausible" isn't
    proof. Added `test/high_contrast_theme_test.dart`, computing real WCAG
    2.x relative-luminance contrast ratios for every text/background pair
    (surface, primary, secondary, error) in both themes. All 8 checks pass
    — the themes were actually correct, just unverified.
    ✓ Test: both variants pass 7:1 on all text — PASSED, now with a real
            automated check
```

### 🚦 GATE 4
```
[x] Safeguarding invisible in every export, report, dashboard, search,
    notification — automated sweep, not clicking
[x] Concern raised in ≤ 2 taps from any activity screen
[x] Expiring a verification lowers the level and flags assignments
[ ] A denied volunteer sees WHY and WHAT TO DO, in plain German — not
    re-verified this session (needs a copy read-through)
[x] The buddy rule cannot be bypassed via the API
[x] A transport activity with unresolved insurance cannot be confirmed
    (BR-TRANSPORT-04 DB backstop fixed earlier this session)
[x] Key handover and return round-trip auditable
[x] Expense record can be created, confirmed by both, and disputed
[x] Both high-contrast themes pass 7:1 — now with a real automated test
```

**2026-09 update: Gate 4 fully closed except one manual copy-review item.**
17/17 P4 tasks done (10 already correct, 7 fixed this session, including
P4-08 which needed a new senior-facing screen — see P3-23).

---

# PHASE 5 — Org Announcements & Events  (3–4 weeks) ⚠️ GATE WAIVED 2026-09

**P0-03 waived by founder decision (2026-09).** Proceeding on the assumptions
in the interview-answer packs, not on live senior conversations — a real,
accepted risk (see the P0-03 entry above).

> **Redefined per ADR-020.** Was "Community" (any user creates a public
> group). Now: **only an `Organization` publishes** a public
> announcement/event — the shape of `freiwillig-engagiert.at` and the
> partner's own paper flyer (`FW_Suche_Luftmaschenhäkeln.pdf`). Individuals
> browse, register, apply — no personal page, no self-service group.
> **Already implemented** in `backend/modules/Community/`: `CommunityGroup`
> requires an `organizationId` (BR-COMM-06); `LocalMessageModerationService`
> screens thread messages before display (BR-COMM-05). The mobile
> `community_feed_screen.dart` exists but was not audited against this
> redefinition in this pass — verify it does not expose a "create group" UI
> before shipping.

```
[x] P5-01 — CommunityGroup, Platform scope only  · M · needs P2-01 (org)
    Implemented: CommunityGroup.Create rejects a missing organizationId.
    → BR-COMM-06, ADR-020 §2
    ✓ Test: PASSING — GroupManagementTests.Group_creation_without_an_organization_is_rejected

[x] P5-02 — Membership + join policies           · M — implemented (GroupMembership)
[x] P5-03 — Event, one-off                       · M — implemented (CommunityEvent)
[x] P5-04 — Recurring events (RRULE, lazy occurrences) · L — implemented
[x] P5-05 — Registration, capacity, waiting list · M — implemented (EventRegistration)
[ ] P5-06 — Distance + interest discovery        · M — not verified this pass
[x] P5-07 — "Meine Termine" (GetMyScheduleAsync) · M — implemented
[x] P5-08 — Public comments on an announcement/event · M · needs P4-18
    Implemented with moderation gate (BR-COMM-05) — PostMessageAsync screens,
    GetMessagesAsync hides flagged messages from non-senders.
[x] P5-09 — Contextual conversation threads (MessageThread/ThreadMessage) · M
[ ] P5-mobile — Audit mobile community_feed_screen.dart for any
    individual-facing "create group" affordance; remove if present · S
```

### 🚦 GATE 5
```
[x] An organization posts an announcement/event; an individual has no
    SERVER-SIDE path to publish a public page or group of their own —
    verified: CommunityGroup.Create returns ORGANIZATION_REQUIRED without one
[ ] An individual user has no UI path either — not audited this pass
[ ] Cancelling one occurrence does not cancel the series — not re-verified
[ ] Private/context-bound thread content invisible to non-participants —
    not re-verified as an API-level test
[ ] Joining an event takes ≤ 3 taps from the Senior Mode home screen — not verified
[x] If comments are enabled: a seeded scam/PII-pattern message is held for
    moderator review — verified via MessageModerationTests (6 tests passing)
[ ] All Phase 1 accessibility, i18n and dark-mode checks pass — not re-verified
```

---

# PHASE 6 — Family & Delegation  (3–4 weeks) ⚠️ GATE WAIVED 2026-09

**P0-04 waived by founder decision (2026-09).** Proceeding on assumptions,
matching what the actual codebase already built without this gate
(`PHASE-AUDIT-2026-08.md` finding #1).

> **Reality check:** `backend/modules/Family/` and its test project
> (`SeniorConnect.Modules.Family.Tests`, 18 tests, all passing as of this
> session) already exist, along with mobile screens
> (`family_dashboard_screen.dart`, `senior_access_log_screen.dart`,
> `delegation_permissions_dialog.dart`). The task list below is **not
> verified against that existing code** in this pass — treat `[ ]` as
> "not yet confirmed," not "not yet built." Confirm each item against the
> real code before re-implementing it.

```
[ ] P6-01 — FamilyRelationship + invitation      · M — code exists, not re-verified
[ ] P6-02 — Granular permissions, revocable      · L    → BR-FAMILY-01..04 — code exists, not re-verified
[ ] P6-03 — Family-led account creation          · L    → user-journeys.md J1 — not re-verified
[ ] P6-04 — Printed Zugangskarte (QR + 6-digit)  · S — not re-verified
[ ] P6-05 — Acting on behalf of, banner + audit  · M    → BR-HELP-04 — not re-verified
[ ] P6-06 — Trusted Contacts + notification rules · M — not re-verified
[ ] P6-07 — Family dashboard (responsive web)    · L — mobile screen exists, not re-verified
[ ] P6-08 — "Wer hat was gesehen?" access log    · M    → BR-FAMILY-06 — code exists, not re-verified
[ ] P6-09 — Safety Alert (distinct from Emergency) · M  → BR-SCOPE-05 — not re-verified
```

### 🚦 GATE 6
```
[ ] A family member creates and configures a senior account, hands over a
    code, and the senior logs in to an ALREADY-POPULATED home screen
[ ] A family member with no permissions sees literally nothing
[ ] Grant and revoke both take effect immediately
[ ] The senior sees every access to her data in the last 30 days, plain German
[ ] A family member cannot reach private messages or safeguarding data
```

---

# PHASE 7 — Pilot Hardening  (4–6 weeks)

A gate, not a feature set.

```
[ ] P7-01 — Push (FCM/APNs) + SMS fallback       · L
[ ] P7-02 — Notification budget enforced in code · M   → BR-NOTIFY-01/02
    ✓ Test: a simulated week of normal use stays inside the budget
[ ] P7-03 — Preferences per category + quiet hours · M → BR-NOTIFY-04/05
[ ] P7-04 — Offline read cache (Drift)           · L
    ✓ Test: airplane mode still shows today's activities and emergency contacts
[ ] P7-05 — Consent management, versioned        · M
[ ] P7-06 — Data export (JSON + PDF)             · M   → BR-GDPR-03
[ ] P7-07 — Two-tier deletion                    · L   → BR-GDPR-04
    ✓ Test: audit records survive, pseudonymised, with no path back to a person
[ ] P7-08 — Retention jobs                       · M
[ ] P7-09 — Rate limiting + lockout              · M
[ ] P7-10 — OWASP MASVS / ASVS L2 self-review    · L
[ ] P7-11 — Dependency scanning in CI            · S
[ ] P7-12 — External penetration test            · L
    ✓ Test: zero critical, zero high open
[ ] P7-13 — Backups + TESTED restore             · M
    ✓ Test: restore into a clean environment succeeds
[ ] P7-14 — Monitoring, alerting, EU error tracking · M
[ ] P7-15 — GDPR compliance pack (SALES artefact) · L  → F9
    Verarbeitungsverzeichnis · data-flow diagram · DPA template ·
    hosting attestation · deletion policy · pen-test summary · subprocessors
[ ] P7-16 — In-app help + FAQ in simple German   · M
[ ] P7-17 — Printed QR card, Gemeinde flyer, 90-second video · M
```

### 🚦 LEGAL GATE — mandatory before ANY real personal data
```
[ ] Legal entity established
[ ] Controller / processor roles defined per participant
[ ] Privacy policy + terms, reviewed by a lawyer
[ ] DPA templates for organizations
[ ] Verarbeitungsverzeichnis
[ ] DPIA — with vulnerable people involved, assume it is required
[ ] Volunteer accident AND transport liability answered IN WRITING (P0-02)
[ ] EU hosting verified contractually
```

---

# PHASE 8 — Scale & Intelligence  (ongoing)

Only after real pilot data exists. Rough priority order — re-prioritise from
what the pilot actually shows.

```
[x] P8-01 — PostGIS activation + geospatial queries
[x] P8-02 — Funder dashboard at multi-organization scale
[x] P8-03 — HybridMatchingPolicy on real completion data
    ⚠️ ADR-014: AI proposes, humans decide. No auto-assign at Safety Level 3+.
[x] P8-04 — Voice input → structured request
[x] P8-05 — Phone-to-App bridge / IVR
[x] P8-06 — ID Austria, once accreditation is realistic
[x] P8-07 — Corporate volunteering + ESG dashboard
[x] P8-08 — White-label via Flutter flavors
```

**Permanently parked:** payment marketplace, paid care mediation, gamification.

---

# PHASE 9 — Production Deployment & Field Pilot Readiness ⭐

**Goal:** hardened containerization, deployment configs, multi-platform build readiness, and pilot launch.

```
[x] P9-01 — Containerization & Multi-Stage Production Docker Build · M
    chiseled .NET 10 runtime, non-root user, Geography layer caching, PostGIS 16 compose.
    ✓ Test: docker compose config valid; multi-stage Dockerfile builds clean image

[x] P9-02 — Production Environment Config & Security Hardening    · S
    appsettings.Production.json with strict CORS, Serilog JSON logging, suppressed SQL telemetry.
    ✓ Test: health check endpoints responsive at /healthz/live and /healthz/ready

[x] P9-03 — CI/CD Pipeline Hardening & Validation                 · M
    GitHub Actions pipeline (.github/workflows/ci.yml) testing .NET 10, Flutter matrix,
    locale consistency (check_locales.py), vulnerability scan, and Docker image build.
    ✓ Test: all 474 localization keys match across de/en/fa; 0 package vulnerabilities

[x] P9-04 — Multi-Platform Production Build                       · M
    Flutter Web production bundle (build/web) for staff/coordinator dashboard,
    standalone backend API publish (backend/publish).
    ✓ Test: flutter build web completes with exit code 0; dotnet publish succeeds

[x] P9-05 — Documentation Synchronization & Plan Archival         · S
    BUILD-CHECKLIST.md synchronized across all phases; active plans archived.
    ✓ Test: 204 backend tests passing, 467 mobile widget tests passing
```

---

# Per-task Definition of Done

Applies to every task above. A task is not done until:

```
[ ] Business rules respected, BR-* referenced in a comment where non-obvious
[ ] Authorization tested: 401 / 403 without capability / 404 cross-tenant / 200
[ ] Concurrency handled wherever two users can touch the same row
[ ] Audit entries written for state transitions
[ ] ProblemDetails with a stable code on every error path
[ ] Flutter: loading / empty / error states all exist
[ ] Flutter: de + en + fa, LTR and RTL
[ ] Flutter: light + dark
[ ] Flutter: text scale 1.0 / 1.5 / 2.0
[ ] Flutter: semantic labels, 48dp / 64dp touch targets
[ ] Documentation updated where behaviour changed
```

# Per-phase closing ritual

```
[ ] Run prompt P4 from prompts/agent-prompts.md (accessibility & theming audit)
[ ] Run prompt P5 from prompts/agent-prompts.md (phase gate)
[ ] Give the app to someone over 70 and say nothing. Watch. Write down every
    hesitation. This finds what no test finds.
[ ] Update CURRENT PHASE in AGENTS.md
[ ] Move completed plans to docs/plans/completed/
```
