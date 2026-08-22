# Build Checklist

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

[ ] P0-03 — Five conversations with seniors      · M   ⚠️ GATES PHASE 5
    Not a survey. Sit with them. Watch them use their phone.
    ✓ Test: five sets of verbatim notes; the Community pillar is confirmed or killed

[ ] P0-04 — Five conversations with adult children · M  ⚠️ GATES PHASE 6
    The family-led onboarding thesis is the strongest unvalidated assumption
    in the entire product.
    ✓ Test: you can state, with evidence, whether a daughter would install this
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

## 1.2 Passwordless identity (ADR-016)

```
[ ] P1-07 — SMS provider adapter                 · M · needs P1-01
    Behind ISmsSender. EU provider. Choose and budget it now — it is a real cost.
    ✓ Test: a code arrives on a real Austrian mobile number

[ ] P1-08 — Request OTP endpoint                 · M · needs P1-03, P1-07
    POST /auth/request-code. Hash the destination and the code, never store either.
    → BR-AUTH-01, BR-AUTH-07
    ✓ Test: 6 requests in a minute for one number → the 6th returns 429

[ ] P1-09 — Verify OTP + issue tokens            · M · needs P1-08
    5-minute expiry, max 5 attempts, 90-day refresh on a personal device.
    → BR-AUTH-04, BR-AUTH-07
    ✓ Test: a wrong code 5 times invalidates the challenge; a 6-minute-old code fails

[ ] P1-10 — Refresh, logout, device list         · S · needs P1-09
    Hashed, revocable per device.
    ✓ Test: logging out one device leaves the other signed in

[ ] P1-11 — Email magic link (alternative path)  · M · needs P1-03
    → BR-AUTH-01
    ✓ Test: a user with no phone number can still sign in

[ ] P1-12 — Staff password + TOTP path           · M · needs P1-03
    ONLY for organization staff and platform admins.
    → BR-AUTH-02
    ✓ Test: creating a phone_otp user with a password_hash is rejected by the DB

[ ] P1-13 — Phone-number change flow             · M · needs P1-09
    Invalidates every session, notifies the OLD number and email, audited.
    → BR-AUTH-06 (SIM-swap mitigation)
    ✓ Test: change the number → the other device is signed out within a minute
```

## 1.3 Profiles & authorization

```
[ ] P1-14 — User + SeniorProfile + VolunteerProfile · M · needs P1-03
    Not mutually exclusive. One user may have both.
    → personas.md, data-model.md §1
    ✓ Test: one user holds both profiles simultaneously

[ ] P1-15 — Reference data + seed                · S · needs P1-03
    interests, languages, skills, availability_slots
    ✓ Test: GET /reference/* returns the seeded rows

[ ] P1-16 — Capability framework                 · L · needs P1-03
    Policy-based, server-side only. Trust levels 0–1 in this phase.
    → authorization.md §2–§4
    ✓ Test: a client sending trustLevel in a request body has it ignored entirely

[ ] P1-17 — Explainable denial                   · M · needs P1-16
    Every 403 returns `missing[]` — what is absent and how to obtain it.
    → authorization.md §5
    ✓ Test: a denied request returns a list a human can act on, not just "Forbidden"

[ ] P1-18 — /me, /me/trust, /me/capabilities     · M · needs P1-16
    ✓ Test: the four authorization tests pass on each (401 / 403 / 404 / 200)
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
    ⚠️ Never load fonts from a CDN — offline breakage plus a GDPR problem.
    → design-system.md
    ✓ Test: a hardcoded Colors.white anywhere in lib/features fails the lint

[ ] P1-21 — Theme mode + Senior Mode persisted   · S · needs P1-20
    Copy starter/flutter/app_settings.dart.
    ✓ Test: set dark + Große Ansicht, kill the app, reopen → both survive

[ ] P1-22 — Localization de/en/fa + RTL          · M · needs P1-19
    Copy starter/i18n/. Wire check_locales.py into CI.
    → BR: German is the source of truth (ADR-012)
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
    Maps ProblemDetails `code`, never the message text.
    ✓ Test: an expired access token refreshes without the user seeing anything

[ ] P1-27 — Auth screens (NO password field)     · M · needs P1-26
    Phone → code → in. Under 30 seconds.
    → BR-AUTH-03. A password field here is a defect.
    ✓ Test: grep the senior/volunteer screens for obscureText — zero hits

[ ] P1-28 — Profile screens                      · M · needs P1-27
    View, edit, interests, languages, availability
    ✓ Test: loading / empty / error states all exist on every screen
```

### 🚦 GATE 1
```
[ ] Phone → SMS code → signed in, under 30 s, no password anywhere
[ ] Close the app, wait a week, reopen → lands on content, not on a login screen
[ ] Changing the phone number kills all sessions and notifies the old number
[ ] OTP brute force throttled (automated test)
[ ] Works in de, en, fa — fa renders RTL, directional icons mirror correctly
[ ] Every screen in light AND dark AND system-follows-OS
[ ] Every screen at text scale 1.0 / 1.5 / 2.0 without overflow
[ ] Senior Mode changes type scale, touch targets and navigation shell
[ ] TalkBack and VoiceOver complete the login flow
[ ] check_locales.py in CI; a missing key fails the build
[ ] No hardcoded user-visible string; no hardcoded Colors.* in lib/features
[ ] Audit log records login and profile change
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
[ ] P2-01 — Organization + Branch + Membership   · L · needs P1-03
    Apply starter/sql/002_phase2_wedge.sql §1 as the reference.
    ⚠️ NO 'municipality' organization type — a Gemeinde is a funder (ADR-017).
    → BR-TENANT-01/02
    ✓ Test: the 4 staff roles exist and safeguarding_officer is separate from admin

[ ] P2-02 — Global query filters + tenant context · M · needs P2-01
    ⚠️ The filter MUST admit OrganizationId == null (ADR-007).
    → starter/backend/src/SeniorConnect.Infrastructure/SeniorConnectDbContext.cs
    ✓ Test: TenantIsolationTests green, including the null-organization test

[ ] P2-03 — Manual cross-tenant attempt          · S · needs P2-02
    Not an automated test — you, with a real token, trying to read Org B.
    → BR-TENANT-03
    ✓ Test: you get 404 (never 403 — a 403 confirms the row exists)

[ ] P2-04 — Organization policies (key/value)    · S · needs P2-01
    Roster thresholds, SLA days, matching weights later.
    → BR-ROSTER-02
    ✓ Test: changing a policy changes behaviour with no code change
```

## 2.2 Activities & hours — the core of the phase

```
[ ] P2-05 — activity_categories + blocked list   · M · needs P1-03
    Including the 6 blocked categories and their referral groups.
    → BR-SCOPE-02
    ✓ Test: every is_blocked row has a referral_group (schema test 6)

[ ] P2-06 — referral_providers, pilot region only · S · needs P2-05
    Hand-curate for ONE Gemeinde. Do not build a national directory.
    ✓ Test: a blocked category resolves to at least 2 real local providers

[ ] P2-07 — Activity aggregate                   · L · needs P2-01, P2-05
    Copy starter/backend/modules/Activities/Domain/Activity.cs.
    Standalone — NOT dependent on help requests, which do not exist yet.
    → data-model.md §11
    ✓ Test: unit tests cover every branch of Log(), Confirm(), Dispute()

[ ] P2-08 — insurance_context on every activity  · M · needs P2-07
    Three values. "Unknown" is visible, never hidden.
    → F4, BR-SAFETY-06
    ✓ Test: an activity cannot be created without an insurance context

[ ] P2-09 — Transport as its own dimension       · M · needs P2-07
    → BR-TRANSPORT-01..05
    ✓ Test: confirming a volunteer_private_vehicle activity with unknown
            insurance is BLOCKED — in the domain AND by the DB constraint

[ ] P2-10 — Log activity endpoint                · M · needs P2-07
    POST /activities. Idempotency-Key honoured.
    ✓ Test: tapping submit three times on a bad connection creates ONE activity

[ ] P2-11 — Blocked category → referral response · M · needs P2-06, P2-10
    Returns a referral. Creates NOTHING. Logs BlockedCategoryReferral.
    → BR-SCOPE-03
    ✓ Test: requesting "Kompressionsstrümpfe anziehen" creates zero rows

[ ] P2-12 — Confirm activity endpoint            · M · needs P2-10
    A volunteer may NOT confirm their own hours.
    → starter/backend/modules/Activities/Application/ConfirmActivityHandler.cs
    ✓ Test: self-confirmation returns 403 SELF_CONFIRMATION_NOT_ALLOWED

[ ] P2-13 — v_volunteer_hours view               · S · needs P2-12
    A VIEW over confirmed activities. Never a second table.
    ✓ Test: the monthly total equals a hand-calculated control set, exactly

[ ] P2-14 — Volunteer self-log screen            · M · needs P2-10, P1-23
    Prefilled from the last entry.
    → F1. This screen is why the whole phase exists.
    ✓ Test: log an activity in 2 taps from the home screen, timed

[ ] P2-15 — Coordinator bulk entry               · M · needs P2-10
    For volunteers who report by phone or on paper.
    ✓ Test: log a completed activity in under 15 seconds, timed

[ ] P2-16 — Monthly reminder to silent volunteers · S · needs P2-13
    → BR-NOTIFY: this counts against the weekly budget
    ✓ Test: a volunteer who logged nothing gets exactly one reminder, not three
```

## 2.3 Roster truth (F2)

```
[ ] P2-17 — Roster status, computed              · M · needs P2-13
    Active / Dormant / Inactive / NeverActivated, from behaviour only.
    → BR-ROSTER-01, thresholds from P2-04
    ✓ Test: status matches a hand-checked sample of 10 volunteers

[ ] P2-18 — Nightly materialized view refresh    · S · needs P2-17
    REFRESH ... CONCURRENTLY (needs the unique index).
    ✓ Test: the refresh does not lock the dashboard

[ ] P2-19 — Reactivation flow for dormant        · M · needs P2-17
    ✓ Test: a dormant volunteer receives one re-engagement message, opt-out honoured

[ ] P2-20 — Reports use ACTIVE, never roster size · S · needs P2-17
    → BR-ROSTER-03. "60 volunteers" when 22 are active is a false statement
      to a funder.
    ✓ Test: no report or dashboard anywhere displays total membership as "active"
```

## 2.4 Verification records (F11)

```
[ ] P2-21 — verifications CRUD + approval        · L · needs P2-01
    Manual + Organization providers only. Outcomes stored, documents NEVER.
    → BR-TRUST-05, trust-safety.md §3
    ✓ Test: there is no column, blob or upload path for a document. Grep for it.

[ ] P2-22 — Digital consent capture              · M · needs P2-21
    Replaces the physical filing cabinet (F1, 15:00–16:30).
    ✓ Test: a signed confidentiality agreement is retrievable and versioned

[ ] P2-23 — Onboarding pipeline                  · L · needs P2-21
    5 steps, SLA per step, days_open COMPUTED never stored.
    → BR-ONBOARD-01..03
    ✓ Test: the applicant sees their own status; a 15-day-old step shows overdue

[ ] P2-24 — Verification expiry + reminders      · M · needs P2-21
    ✓ Test: expiry lowers the trust level and raises a coordinator task
```

## 2.5 Coordinator dashboard (web)

```
[ ] P2-25 — Staff web app shell                  · L · needs P2-02
    Blazor or React. Keyboard-navigable, screen-reader usable.
    ✓ Test: complete one full task using only the keyboard

[ ] P2-26 — "Braucht heute Aufmerksamkeit"       · L · needs P2-17, P2-23
    FIRST widget, above everything: dormant · unconfirmed hours ·
    waiting applications · expiring verifications · unresolved insurance
    → roadmap.md Phase 2
    ✓ Test: loads in under 2 s with 5 000 seeded activities

[ ] P2-27 — Volunteer list with roster status    · M · needs P2-17
    ✓ Test: filter by status returns the same set as the hand-checked sample

[ ] P2-28 — Hours confirmation queue             · M · needs P2-12
    ✓ Test: confirm 20 activities in under 2 minutes
```

## 2.6 Impact reporting — what they actually buy

```
[ ] P2-29 — Impact metrics                       · L · needs P2-13
    hours · people supported · activities by type · by age band ·
    active volunteers · repeat participation · fulfilment rate ·
    "how often coordination stepped in itself" (F12)
    ✓ Test: every number in F1 §5 is derivable with no spreadsheet

[ ] P2-30 — PDF export                           · M · needs P2-29
    → This is the artefact that closes the sale.
    ✓ Test: the PDF numbers match the database exactly, row by row

[ ] P2-31 — CSV/XLSX export                      · S · needs P2-29
    ✓ Test: opens cleanly in Excel with German number formatting
```

## 2.7 Funder surface (ADR-017)

```
[ ] P2-32 — funders + funding_relationships      · M · needs P2-01
    ⚠️ No FK from anything here to a user-level table.
    ✓ Test: grep the funder schema for user_id — zero hits

[ ] P2-33 — Separate /api/v1/funder namespace    · L · needs P2-32
    Dedicated aggregate queries. NOT the org endpoints with a filter.
    → BR-FUNDER-06
    ✓ Test: FunderApiSurfaceTests green, walking the whole DTO object graph

[ ] P2-34 — Cohort suppression, minimum 10       · M · needs P2-33
    Suppress EVERY measure in the row, not just the headcount.
    → BR-FUNDER-03
    ✓ Test: a 7-person cohort shows "<10" AND cannot be recovered by
            subtracting two other cells

[ ] P2-35 — Funder dashboard (read-only)         · M · needs P2-33
    ✓ Test: with a real funder token, you cannot reach one name
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

# PHASE 3 — Help & Matching  (5–7 weeks)

**Goal:** the core loop. After this you have something to demo.

## 3.1 Help requests

```
[ ] P3-01 — HelpRequest aggregate + state machine · L · needs P2-07
    → BR-HELP-01/02. Only legal transitions; anything else is 409.
    ✓ Test: every illegal transition returns INVALID_STATE_TRANSITION

[ ] P3-02 — IActivitySafetyPolicy                · L · needs P3-01
    Safety level determined SERVER-SIDE from category + full context.
    → BR-SAFETY-01/04
    ✓ Test: a client-sent safety level is ignored entirely

[ ] P3-03 — Create request endpoint              · M · needs P3-01
    ✓ Test: the four authorization tests pass

[ ] P3-04 — Blocked category → referral          · S · needs P2-11, P3-03
    Reuses the Phase 2 referral flow.
    ✓ Test: no help_request row is created

[ ] P3-05 — Emergency detection → emergency screen · M · needs P3-03
    → BR-SCOPE-04/05. Routes to 144/112. NEVER says help is on the way.
    ✓ Test: an emergency phrase opens the emergency screen, not a form

[ ] P3-06 — Cancellation with reason codes       · S · needs P3-01
    → BR-HELP-05
    ✓ Test: cancelling without a reason is rejected

[ ] P3-07 — status_history table                 · S · needs P3-01
    ✓ Test: every transition leaves a row with actor and reason
```

## 3.2 Matching

```
[ ] P3-08 — Hard filters (SQL, set-based)        · L · needs P3-02
    A failing candidate is ABSENT, never low-ranked.
    → matching-engine.md §2
    ✓ Test: a volunteer one trust level short never appears (service-level test)

[ ] P3-09 — RuleBasedMatchingPolicy              · L · needs P3-08
    Weights from configuration, never constants.
    → matching-engine.md §3
    ✓ Test: changing a weight in config changes the ranking, no rebuild

[ ] P3-10 — Score breakdown / explainability     · M · needs P3-09
    ✓ Test: the breakdown sums to the total; a coordinator sees one-sentence why

[ ] P3-11 — New-volunteer cold start             · S · needs P3-09
    reliability defaults to 0.7, not 0.
    ✓ Test: a brand-new volunteer can win a first assignment

[ ] P3-12 — Continuity factor                    · S · needs P3-09
    Seniors overwhelmingly prefer the same person again.
    ✓ Test: a prior positive pairing outranks a marginally closer stranger

[ ] P3-13 — Tiered offers, NOT broadcast         · L · needs P3-09
    3 → 7 → all → escalate to coordinator (F12)
    → matching-engine.md §6
    ✓ Test: 10 eligible volunteers produce 3 notifications, not 10
```

## 3.3 Assignment & completion

```
[ ] P3-14 — Atomic accept                        · L · needs P3-13
    Conditional UPDATE ... WHERE status = 'offered'. RowsAffected = 0 → 409.
    → BR-HELP-03, ADR-006
    ✓ Test: two simultaneous accepts — exactly one wins, the other gets a
            clear, non-blaming message and 3 alternatives

[ ] P3-15 — Contact details revealed post-assignment · S · needs P3-14
    → BR-COMM-04
    ✓ Test: before assignment, the API returns no address and no phone number

[ ] P3-16 — Check-in / check-out                 · M · needs P3-14
    Time-bounded, activity-scoped. NO background location, ever.
    ✓ Test: the app requests no ACCESS_BACKGROUND_LOCATION permission

[ ] P3-17 — Completion → Activity                · M · needs P3-16, P2-07
    A completed request PRODUCES a Phase 2 Activity. One model, not two.
    ✓ Test: completing a request makes hours appear in the Phase 2 report
            with no extra step

[ ] P3-18 — Reminders T-24h / T-2h               · M · needs P3-14
    One-tap "Ich komme" / "Ich schaffe es nicht".
    → BR-NOTIFY-01: exactly 2 pushes per assignment
    ✓ Test: an assignment produces exactly 2 pushes over its lifetime

[ ] P3-19 — No-show with dispute                 · M · needs P3-17
    → BR-HELP-06. Only 30 min after start; disputed no-shows do not count.
    ✓ Test: a successful dispute reverts the reliability score

[ ] P3-20 — Reliability score                    · M · needs P3-19
    Behaviour only. Users see a WORD, never a number.
    → trust-safety.md §8, ADR-009
    ✓ Test: no public star rating exists anywhere in the UI
```

## 3.4 Flutter

```
[ ] P3-21 — Senior request flow                  · L · needs P3-03, P1-24
    6 picture cards → when → optional note → one review screen → submit
    → user-journeys.md J2
    ✓ Test: home screen to submitted request in at most 5 taps

[ ] P3-22 — Volunteer feed                       · L · needs P3-09
    "In Ihrer Nähe" with a near-my-home filter (the volunteer asked for this).
    Ineligible items greyed WITH the reason and the path to eligibility.
    ✓ Test: an ineligible card explains what is missing, in plain German

[ ] P3-23 — Assignment + check-in screens        · M · needs P3-16
    ✓ Test: complete the full loop in Senior Mode with TalkBack on

[ ] P3-24 — Emergency screen                     · M · needs P3-05
    Huge buttons. Two-step confirm. Never claims help is coming.
    ✓ Test: copy review — no sentence implies dispatch
```

### 🚦 GATE 3
```
[ ] Senior creates → volunteer accepts → checks in → completes → hours appear
    in the Phase 2 report with no extra step
[ ] Two simultaneous accepts: exactly one wins, clear 409 for the other
[ ] A volunteer below the required trust level never appears as a candidate
[ ] Blocked category → referral, zero rows created
[ ] An emergency phrase opens the emergency screen, not a request form
[ ] Matching weights configurable without a code change
[ ] Escalation reaches the coordinator when tiers 1–3 produce nothing
[ ] Full loop completable in Senior Mode with TalkBack enabled
[ ] Exactly 2 pushes per assignment
```

---

# PHASE 4 — Trust & Safeguarding  (4–5 weeks)

**Goal:** make a pilot with real people defensible.

```
[ ] P4-01 — Trust levels 0–5 computed            · L
    Deterministic, side-effect free, snapshotted.
    → BR-TRUST-02/03, trust-safety.md §2
    ✓ Test: the same inputs always produce the same level; a snapshot is written

[ ] P4-02 — IIdentityVerificationProvider        · M · needs P4-01
    Manual + Organization. ID Austria is a LATER implementation, not a dependency.
    → BR-TRUST-06, ADR-013
    ✓ Test: adding a provider is a DI registration change and nothing else

[ ] P4-03 — Expiry lowers level, flags assignments · M · needs P4-01
    → BR-TRUST-07. Never auto-cancels without a human seeing it.
    ✓ Test: expiry raises a coordinator task, does not silently cancel

[ ] P4-04 — Capability engine full               · L · needs P4-01
    PerformSafetyLevel1..5 derived, never hand-granted.
    ✓ Test: granting SafetyLevel4 by hand is impossible through the API

[ ] P4-05 — Trust badges in the UI               · M · needs P4-01
    FACTUAL, never evaluative. Copy starter/flutter TrustBadge.
    → BR-TRUST-04
    ✓ Test: copy review — no badge anywhere says "safe" or "100 %"

[ ] P4-06 — Safety levels enforced in matching   · M · needs P4-04, P3-08
    ✓ Test: the reason for exclusion is surfaced to the volunteer

[ ] P4-07 — Buddy System, first 3 Level-3+       · M · needs P4-04
    → BR-SAFETY-05. Checked at matching AND re-checked at assignment.
    ✓ Test: calling accept directly cannot bypass it

[ ] P4-08 — First Meeting Protocol               · S · needs P4-07
    ✓ Test: both parties see the checklist before a first Level-3+ meeting

[ ] P4-09 — Safeguarding schema + DbContext      · L
    Separate PostgreSQL schema, separate DbContext, separate DB grants.
    → ADR-004, BR-SG-01
    ✓ Test: SafeguardingIsolationTests green

[ ] P4-10 — Concern reporting, ≤2 taps           · M · needs P4-09
    → BR-SG-04
    ✓ Test: raise a concern from any activity screen in 2 taps

[ ] P4-11 — Case workflow + restricted access    · L · needs P4-09
    OrganizationAdmin does NOT imply access.
    → BR-SG-02
    ✓ Test: an admin without the capability gets 403 on EVERY endpoint

[ ] P4-12 — Case access log                      · S · needs P4-11
    → BR-SG-06
    ✓ Test: every read writes a row

[ ] P4-13 — Automated leak sweep                 · M · needs P4-11
    → BR-SG-05
    ✓ Test: safeguarding appears in NO export, report, dashboard, search
            or notification — asserted by a test, not by clicking

[ ] P4-14 — Key custody records                  · M
    → F5, BR-KEYS-01..05. No key codes, no photos of keys.
    ✓ Test: handover → return round-trip is auditable; an inactive volunteer
            still holding a key raises a coordinator task

[ ] P4-15 — Expense records                      · M
    → F6, BR-EXPENSE-01..05
    ✓ Test: given − spent ≠ returned without a note is rejected;
            either party can dispute

[ ] P4-16 — Block & report between users         · S
    → BR-COMM-03
    ✓ Test: a blocked user never appears in matching, in either direction

[ ] P4-17 — High-contrast themes                 · M
    → design-system.md §2.4
    ✓ Test: both variants pass 7:1 on all text
```

### 🚦 GATE 4
```
[ ] Safeguarding invisible in every export, report, dashboard, search,
    notification — automated sweep, not clicking
[ ] Concern raised in ≤ 2 taps from any activity screen
[ ] Expiring a verification lowers the level and flags assignments
[ ] A denied volunteer sees WHY and WHAT TO DO, in plain German
[ ] The buddy rule cannot be bypassed via the API
[ ] A transport activity with unresolved insurance cannot be confirmed
[ ] Key handover and return round-trip auditable
[ ] Expense record can be created, confirmed by both, and disputed
[ ] Both high-contrast themes pass 7:1
```

---

# PHASE 5 — Community  (3–4 weeks) ⚠️ BLOCKED ON P0-03

**Do not start until five real seniors have been interviewed.**
This pillar currently has zero evidence behind it.

```
[ ] P5-00 — Re-read the senior interview notes and CUT what nobody wanted · S
    ✓ Test: at least one planned feature below is deleted, not built

[ ] P5-01 — CommunityGroup + scopes              · M
[ ] P5-02 — Membership + join policies           · M
[ ] P5-03 — Event, one-off                       · M
[ ] P5-04 — Recurring events (RRULE, lazy occurrences) · L
[ ] P5-05 — Registration, capacity, waiting list · M
[ ] P5-06 — Distance + interest discovery        · M
[ ] P5-07 — "Meine Termine" (vertical list in Senior Mode, never a grid) · M
[ ] P5-08 — Contextual conversation threads      · M
```

### 🚦 GATE 5
```
[ ] A senior creates a recurring group with NO organization involved
[ ] Cancelling one occurrence does not cancel the series
[ ] Private group content invisible to non-members — API-level test
[ ] Joining an event takes ≤ 3 taps from the Senior Mode home screen
[ ] All Phase 1 accessibility, i18n and dark-mode checks pass
```

---

# PHASE 6 — Family & Delegation  (3–4 weeks) ⚠️ BLOCKED ON P0-04

**Do not start until five real family caregivers have been interviewed.**
This is the strongest unvalidated assumption in the entire product.

```
[ ] P6-00 — Re-read the family interview notes; confirm or kill the thesis · S
    ✓ Test: you can state with evidence whether a daughter would install this

[ ] P6-01 — FamilyRelationship + invitation      · M
[ ] P6-02 — Granular permissions, revocable      · L    → BR-FAMILY-01..04
[ ] P6-03 — Family-led account creation          · L    → user-journeys.md J1
[ ] P6-04 — Printed Zugangskarte (QR + 6-digit)  · S
[ ] P6-05 — Acting on behalf of, banner + audit  · M    → BR-HELP-04
[ ] P6-06 — Trusted Contacts + notification rules · M
[ ] P6-07 — Family dashboard (responsive web)    · L
[ ] P6-08 — "Wer hat was gesehen?" access log    · M    → BR-FAMILY-06
[ ] P6-09 — Safety Alert (distinct from Emergency) · M  → BR-SCOPE-05
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
[ ] P8-01 — PostGIS activation + geospatial queries
[ ] P8-02 — Funder dashboard at multi-organization scale
[ ] P8-03 — HybridMatchingPolicy on real completion data
    ⚠️ ADR-014: AI proposes, humans decide. No auto-assign at Safety Level 3+.
[ ] P8-04 — Voice input → structured request
[ ] P8-05 — Phone-to-App bridge / IVR
[ ] P8-06 — ID Austria, once accreditation is realistic
[ ] P8-07 — Corporate volunteering + ESG dashboard
[ ] P8-08 — White-label via Flutter flavors
```

**Permanently parked:** payment marketplace, paid care mediation, gamification.

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
