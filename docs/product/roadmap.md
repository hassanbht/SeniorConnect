# Roadmap — Phases, Scope and Test Gates

> **v2 — re-cut after discovery. See ADR-015 for why the order changed.**
>
> Each phase is independently shippable and independently testable. You test each
> phase yourself before the next one starts. A phase is not finished until its
> Test Gate passes.
>
> Durations assume one experienced developer working with an AI coding agent,
> part-time. They are estimates, not commitments.

---

## What changed from v1, and why

| Moved | From | To | Reason |
| --- | --- | --- | --- |
| Coordinator wedge (roster, hours, verification records, impact report) | Phase 6 | **Phase 2** | F1 — the only pain all three interviewed roles named |
| Trust & Safeguarding | Phase 5 | Phase 4 | Real assignments start in Phase 3; verification cannot be four phases behind them |
| Community | Phase 2 | Phase 5 | Zero interview evidence — no seniors were interviewed |
| Family & Delegation | Phase 4 | Phase 6 | Zero interview evidence — no family members were interviewed |
| `insurance_context` | Phase 5 | **Phase 2** | F4 — an insurance dispute has already happened in the wild |
| Passwordless auth | not planned | **Phase 1** | F3 — both prior tool failures were login failures |

This is a sequencing change, not a change of vision. `organization_id` stays
nullable throughout and an architecture test enforces it, so the org-optional
thesis (ADR-007) survives the re-cut.

---

## Overview

| Phase | Name | Duration | Shippable outcome |
| --- | --- | --- | --- |
| 0 | Discovery & Project Brain | 2–4 w | Documented reality + frozen MVP scope |
| 1 | Foundation | 4–5 w | Passwordless login, profiles, theming, i18n, dark mode, audit |
| 2 | **Coordinator Wedge** | 5–7 w | Roster truth, activity log, hours, impact PDF. **First sellable thing** |
| 3 | Help & Matching | 5–7 w | The core loop. **First demoable product** |
| 4 | Trust & Safeguarding | 4–5 w | Verification, safety levels, safeguarding, keys, expenses |
| 5 | Community | 3–4 w | Groups & events — the differentiator |
| 6 | Family & Delegation | 3–4 w | Family-led onboarding — the growth engine |
| 7 | Pilot Hardening | 4–6 w | GDPR, notifications, offline, monitoring. **Real pilot ready** |
| 8 | Scale & Intelligence | ongoing | Funder dashboard at scale, AI matching, voice, ID Austria, corporate |

Total to a real pilot: roughly **8–11 months** part-time.

---

## Phase 0 — Discovery & Project Brain

**Goal:** stop designing from assumptions.

Partially complete. `docs/product/discovery-findings.md` holds twelve findings
from three **simulated** personas. Two load-bearing gaps remain:

```
⚠️ No senior was interviewed        → the Community pillar has zero evidence
⚠️ No family member was interviewed → the family-led onboarding thesis is unvalidated
```

### Remaining Phase 0 work

```
[ ] Verify F1, F2, F3, F7, F9 with ONE real coordinator — one call covers all five
[ ] Verify F4 with an insurance broker
[ ] 5 real conversations with seniors        ← gates Phase 5
[ ] 5 real conversations with adult children ← gates Phase 6
[ ] One pilot Gemeinde identified and contacted
[ ] The pilot partner's budget calendar written down (F9 — timing beats features)
[ ] MVP scope frozen in docs/plans/mvp-scope.md
[ ] Repo + CI green on an empty solution
```

The twelve interview questions are in `discovery-findings.md` §Verification plan.
Phases 1–4 may begin now. **Phase 5 and Phase 6 are blocked** until the senior and
family conversations happen.

---

## Phase 1 — Foundation

**Goal:** an app you can log into that looks and feels right, in three languages,
in both themes. No business features yet.

### Backend

- ASP.NET Core 10 modular-monolith skeleton, PostgreSQL + EF Core, migrations, seed
- **Identity: passwordless (ADR-016)** — phone + SMS OTP primary, email magic link
  alternative, email+password+TOTP for staff only
- 90-day refresh tokens, hashed, revocable per device; OTP rate limiting per number
  and per IP; a phone-number change invalidates all sessions and notifies both channels
- `Users` + `Profiles`: `User`, `SeniorProfile`, `VolunteerProfile`, interests,
  languages, skills, availability
- Capability/authorization framework (policy-based, server-side only)
- `TrustLevel` computed service (levels 0–1)
- `Audit` module: append-only, interceptor-based, `REVOKE UPDATE, DELETE` at the DB
- RFC 7807 ProblemDetails, OpenAPI, `/api/v1`, health checks, Serilog, correlation IDs
- Consent capture at registration (versioned)

### Flutter

- Scaffold, feature-first, flavors (`dev` / `staging` / `prod`)
- **Design system**: `AppColors`, `AppTypography`, `AppSpacing`, `AppRadius`,
  `AppBreakpoints`, `AppTheme` — drafted in `starter/flutter/`
- **Dark mode + system mode + persisted preference**
- **Senior Mode („Große Ansicht")** and its separate navigation shell
- `easy_localization` de/en/fa, German source of truth, RTL verified,
  `check_locales.py` wired into CI
- go_router, get_it/injectable, Cubit + Freezed
- API client with silent refresh and typed errors
- **Auth: enter phone → receive code → in.** No password screen anywhere in the
  senior or volunteer path
- Profile screens; shared widget set

### Test Gate 1

```
[ ] Phone → SMS code → logged in, under 30 seconds, no password anywhere
[ ] Close the app, wait a week, reopen: lands on content, not on a login screen
[ ] Changing the phone number kills all sessions and notifies the old number
[ ] OTP brute force is throttled (automated test)
[ ] Works in de, en, fa — fa renders RTL correctly, icons mirror appropriately
[ ] Every screen in light AND dark AND system-follows-OS
[ ] Every screen at text scale 1.0 / 1.5 / 2.0 without overflow
[ ] Senior Mode changes type scale, touch targets and navigation shell
[ ] TalkBack and VoiceOver can complete the login flow
[ ] check_locales.py passes in CI; a deliberately missing key fails the build
[ ] No hardcoded user-visible string; no hardcoded Colors.* in lib/features
[ ] Audit log records login and profile change
[ ] Builds on Android, iOS and Web
```

---

## Phase 2 — Coordinator Wedge ⭐

**Goal:** replace the coordinator's Excel file. This is the phase that gets a
pilot partner to sign, and the phase everything else feeds on.

> Target outcome, in her words: *"December takes an afternoon instead of two
> weeks, and I stopped chasing people for their hours."*

### Organizations & roster

- `Organization`, `OrganizationBranch`, `OrganizationMembership`, staff roles
  (Staff / Coordinator / Admin / **SafeguardingOfficer as a separate role**)
- Multi-tenancy activated: global query filters + architecture tests proving
  isolation, with `organization_id` **nullable** and a test asserting that too
- **Roster truth (F2)**: volunteer status derived from behaviour, never manual —
  `Active` / `Dormant` (60–120 d) / `Inactive` (>120 d) / `NeverActivated`
- Reactivation flow for dormant volunteers

### Activity logging & hours (F1 — the core of this phase)

- `Activity` as a minimal, standalone record: who, for whom, what, when, how long.
  Deliberately **not** dependent on the help-request workflow, which does not exist
  until Phase 3 — a coordinator must be able to log "Anna visited Frau Müller,
  Tuesday, 90 minutes" on day one
- Volunteer self-logging: one screen, one tap, prefilled from the last entry
- Coordinator bulk entry for volunteers who report by phone or on paper
- Coordinator confirmation of self-logged hours
- Monthly reminder to volunteers who logged nothing
- **`insurance_context` on every activity (F4)** — `OrganizationCovered` /
  `PrivateNeighbourly` / `Unknown`, shown before confirmation, never hidden

### Verification records (F11)

- `verifications` with manual and organization providers
- Digitised consent and confidentiality-agreement capture, replacing the physical file
- **Onboarding pipeline with a visible timer (F10)** — the applicant sees their own
  status; the coordinator sees how long each candidate has waited
- Expiry tracking and reminders

### Coordinator dashboard (web)

First widget, above everything else: **„Braucht heute Aufmerksamkeit"**

```
· Freiwillige, die sich seit 90 Tagen nicht gemeldet haben        (F2)
· Stunden, die noch nicht bestätigt sind                          (F1)
· Bewerbungen, die länger als 14 Tage warten                      (F10)
· Verifizierungen, die diesen Monat ablaufen
· Aktivitäten ohne geklärten Versicherungsstatus                  (F4)
```

### Impact reporting

- Metrics: hours, people supported, activities by type, activities by age band,
  active volunteers, repeat participation, fulfilment rate, and
  **"how often coordination had to step in itself"** (F12)
- One-click PDF; CSV/XLSX export
- Everything the coordinator listed for her annual report (F1 §5) must be
  derivable without touching a spreadsheet

### Funder surface (ADR-017)

- `funders`, `funding_relationships`
- Separate `/api/v1/funder/...` namespace, aggregate-only, **minimum cohort 10**
- Architecture test: no DTO reachable from the funder namespace contains a field
  classified `PersonalData` or above

### Test Gate 2

```
[ ] A coordinator logs a completed activity in under 15 seconds
[ ] A volunteer logs their own hours in 2 taps from the home screen
[ ] The monthly hours total matches a hand-calculated control set exactly
[ ] The annual PDF contains every number named in F1 §5, with no manual work
[ ] Roster status is computed from behaviour and matches a hand-checked sample
[ ] Every activity has an insurance context; "Unknown" is visible, not hidden
[ ] A waiting applicant can see their own pipeline status
[ ] Org A cannot read one row of Org B — architecture test AND a manual attempt
      with a real token
[ ] A funder token cannot reach any name, address, phone or free-text field —
      automated test over the whole funder namespace
[ ] A funder aggregate covering 7 people renders "<10" and cannot be inferred by
      subtracting two other cells
[ ] An OrganizationAdmin without the SafeguardingOfficer role gets 403 on every
      safeguarding endpoint, even though the module is nearly empty in this phase
[ ] Dashboard loads in < 2 s with 5 000 seeded activities
[ ] Staff web app is keyboard-navigable and screen-reader usable
```

### The real gate

Sit next to a coordinator while she does one month's reporting in the tool.
If it does not visibly save her time, do not start Phase 3 — fix Phase 2.

---

## Phase 3 — Help & Matching

**Goal:** the core loop. After this you have something to demo.

- `HelpRequest`: create, categories, time window, location, recurrence
- Category catalogue with server-side safety mapping (`IActivitySafetyPolicy`)
- **Blocked-category detection → referral flow** (BR-SCOPE-02/03)
- **Emergency detection → emergency screen** (BR-SCOPE-04)
- Full state machine with legal-transition enforcement
- Rule-based matching (`IVolunteerMatchingService` → `RuleBasedMatchingPolicy`),
  configurable weights, hard filters first, explainable score breakdown
- **Tiered offers, not broadcast** — 3 → 7 → all → escalate to coordinator (F12)
- Volunteer feed: "Anfragen in Ihrer Nähe", with time and effort estimate and the
  "close to my home" filter the volunteer explicitly asked for (F3 §Q10)
- Atomic accept via conditional UPDATE
- Check-in / check-out, time-bounded, no background tracking
- Reminders at T-24h and T-2h with one-tap „Ich komme" / „Ich schaffe es nicht"
- **`Activity` from Phase 2 becomes the completion record** — one model, not two
- Reliability score computed from behaviour
- Cancellation reasons; no-show with dispute

### Test Gate 3

```
[ ] Senior creates → volunteer sees → accepts → checks in → completes → hours
      appear in the Phase 2 report with no extra step
[ ] Two simultaneous accepts: exactly one wins, the other gets a clear 409
[ ] A volunteer below the required trust level never appears as a candidate
[ ] Blocked category → referral, and NO request is created
[ ] An emergency phrase opens the emergency screen, not a request form
[ ] Matching weights change behaviour via configuration, no code change
[ ] Escalation reaches the coordinator when tiers 1–3 produce nothing
[ ] Full loop completable in Senior Mode with TalkBack on
```

---

## Phase 4 — Trust & Safeguarding

**Goal:** make a pilot with real people defensible. Pulled forward from v1
because Phase 3 creates real meetings between real people.

- Trust levels 0–5, computed, with expiry and re-verification
- `IIdentityVerificationProvider` + Manual + Organization implementations
- Capability engine with **explainable denial** — every 403 says what is missing
  and how to obtain it
- Trust badges with plain-language explanations
- Safety levels enforced in matching, reason surfaced to the user
- **BR-TRANSPORT** — private-vehicle transport as its own risk dimension (F4)
- **Buddy System** for the first three Level-3+ activities
- First Meeting Protocol
- **Safeguarding module** — concern in ≤2 taps, restricted case, separate schema,
  assignment, notes, actions, resolution, dedicated audit
- New category from discovery: **`Vorwurf / Missverständnis`** (F6)
- **Key custody records** (F5) — who holds which key, since when, returned when
- **Expense records** on shopping activities (F6) — given, spent, returned,
  optional receipt photo, both parties confirm
- Block & report
- High-contrast themes

### Test Gate 4

```
[ ] Safeguarding is invisible in every export, report, dashboard, search and
      notification — verified by an automated sweep, not by clicking
[ ] A concern can be raised in ≤ 2 taps from any activity screen
[ ] Expiring a verification lowers the trust level and flags affected assignments
[ ] A denied volunteer sees WHY and WHAT TO DO, in plain German
[ ] The buddy rule cannot be bypassed via the API
[ ] A transport activity without a resolved insurance context cannot be confirmed
[ ] A key handover and return round-trip is recorded and auditable
[ ] An expense record can be created, confirmed by both parties, and disputed
[ ] Both high-contrast themes pass 7:1 on all text
```

---

## Phase 5 — Community ⚠️ BLOCKED

**Do not start until five real seniors have been interviewed.** This pillar
currently has zero evidence behind it.

- `CommunityGroup` with scope (Platform / Community / Organization / Private)
- Membership, join policies, group roles
- `Event`: one-off and recurring (RRULE, occurrences materialised lazily)
- Registration, capacity, waiting list, cancellation
- Interest and distance discovery (Haversine; PostGIS-ready columns)
- "Meine Termine" — a plain vertical list in Senior Mode, never a grid calendar
- Contextual conversation threads

### Test Gate 5

```
[ ] A senior creates a recurring group with no organization involved
[ ] Cancelling one occurrence does not cancel the series
[ ] Private group content invisible to non-members — API-level test
[ ] Joining an event takes ≤ 3 taps from the Senior Mode home screen
[ ] All Phase 1 accessibility, i18n and dark-mode checks pass on new screens
```

---

## Phase 6 — Family & Delegation ⚠️ BLOCKED

**Do not start until five real family caregivers have been interviewed.**
This is the strongest unvalidated assumption in the whole product brief.

- Family relationship: invite, senior confirms (or authorised staff on their behalf)
- Granular, revocable `FamilyPermission` set
- **Family-led account creation** + printed Zugangskarte with QR and code
- Acting on behalf of: visible banner, both user IDs recorded
- Trusted Contacts with per-event notification rules
- Family dashboard (responsive web)
- **„Wer hat was gesehen?"** — the senior's own plain-language access log
- Safety Alert workflow, distinct from Emergency

### Test Gate 6

```
[ ] A family member creates and configures a senior account, hands over a code,
      and the senior logs in to an already-populated home screen
[ ] A family member with no permissions can see literally nothing
[ ] Grant and revoke both take effect immediately
[ ] The senior sees every access to her data in the last 30 days, in plain German
[ ] A family member cannot reach private messages or safeguarding data — API test
```

---

## Phase 7 — Pilot Hardening

A gate, not a feature set.

- Push + **SMS fallback**; **notification budget enforced in code** (BR-NOTIFY, F3)
- Preferences per category and channel; quiet hours
- Offline read cache: today's activities, contacts, appointments, emergency info
- Consent management, data export, two-tier deletion, retention jobs
- Rate limiting, brute-force protection, lockout
- OWASP MASVS / ASVS L2 review; dependency scanning; external pen test
- Backups + tested restore; DR runbook
- Monitoring, alerting, EU-hosted error tracking
- **GDPR compliance pack as a sales artefact (F9)** — processing register,
  data-flow diagram, DPA template, hosting attestation, deletion policy,
  pen-test summary. Both buying processes ask for this.
- Onboarding materials: printed QR card, Gemeinde flyer, 90-second video

### Legal gate — mandatory before real data

```
[ ] Legal entity established
[ ] Controller / processor roles defined per participant
[ ] Privacy policy + terms, reviewed by a lawyer
[ ] DPA templates for organizations
[ ] Verarbeitungsverzeichnis
[ ] DPIA — with vulnerable people involved, assume it is required
[ ] Volunteer accident AND transport liability answered in writing (F4)
[ ] EU hosting verified contractually
```

### Test Gate 7

```
[ ] Airplane mode: today's activities and emergency contacts still visible
[ ] Export then delete: audit records survive, pseudonymised
[ ] Restore from backup into a clean environment succeeds
[ ] Pen test: zero critical, zero high open
[ ] Notification budget holds — a week of normal use produces at most the
      budgeted number of pushes, asserted by an automated simulation
```

---

## Phase 8 — Scale & Intelligence

Only after real pilot data exists.

1. PostGIS activation and proper geospatial candidate queries
2. Funder dashboard at multi-organization scale
3. AI-assisted matching (`HybridMatchingPolicy`) — proposes only, never auto-assigns
   Safety Level 3+ (ADR-014)
4. Voice input → structured help request
5. Phone-to-App bridge / IVR for seniors without smartphones
6. ID Austria, once accreditation is realistic
7. Corporate volunteering + ESG dashboard
8. White-label via Flutter flavors

Permanently parked: payment marketplace, paid care mediation, gamification.

---

## Scope discipline

Every new idea goes to `docs/plans/backlog.md` with a date and a one-line
rationale. It does not go into the current phase. The single biggest failure mode
for this project is a 40-feature v1 that never ships.
