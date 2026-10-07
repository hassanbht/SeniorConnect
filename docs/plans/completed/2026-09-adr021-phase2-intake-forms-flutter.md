# ADR-021 Phase 2 + Flutter — Dynamic Org Intake Forms & Auth/Profile Screens

Status: **active** · Started: 2026-09-12
References: `ADR-021`, `BR-ORG-FORM-01..05`, `BR-AUTH-01..03`, `BR-GEO-02..04`, `BUILD-CHECKLIST.md` tasks `P2-37..41`, `P1-27`, `P1-28`.

## Goal

Implement the remaining ADR-021 scope:
- **Backend Phase 2**: Dynamic Organization Intake Forms (`P2-37` through `P2-41`)
- **Flutter Phase 1**: Multi-provider auth screen (`P1-27`) and Austrian profile screen (`P1-28`)

## Non-goals

- Real third-party credentials (Google, ID Austria, BEV/Nominatim) — stubs remain
- Full 2,093-Gemeinden dataset — pilot region (Tirol) only
- Cross-module queries beyond existing contracts

## Phase 2 Backend: Dynamic Organization Intake Forms

### Domain model (new entities)

```
organization_intake_forms
  id · organization_id · form_type (volunteer | help_seeker)
  title · description · is_active bool · version int DEFAULT 1
  created_at_utc · updated_at_utc

intake_form_sections
  id · form_id · title · description · sort_order int

intake_form_fields
  id · section_id · field_key · label_key
  field_type (text | textarea | single_choice | multi_choice | boolean | date | time_slots)
  is_required bool DEFAULT false   -- BR-ORG-FORM-02
  options_json jsonb               -- e.g. categories, target groups, time slots
  sort_order int

intake_form_submissions
  id · form_id · organization_id · user_id
  status (draft | submitted | approved | declined)
  submission_data_json jsonb       -- answers by field_key
  criminal_clearance_declared bool -- BR-ORG-FORM-04
  criminal_clearance_declared_at_utc timestamptz
  gdpr_consent_accepted bool       -- BR-ORG-FORM-05
  gdpr_consent_accepted_at_utc timestamptz
  event_invitation_opt_in bool
  submitted_at_utc · decided_at_utc · decided_by_user_id · review_notes
```

### FWZ Innsbruck-Land Standard Template (pre-seeded)

**Sections & Fields:**
1. **Bereiche** (multi_choice, required) — Soziales, Natur, E-Volunteering, Klima/Nachhaltigkeit, Handwerk, Kunst/Kultur, Freiwilligenpool, Lernbetreuung
2. **Personengruppen** (multi_choice, required) — Geflüchtete, Familien, Senior:innen, Menschen mit Behinderung, Kinder/Jugendliche, Sonstige
3. **Zeitaufwand** (single_choice + time_slots, required) — einmalig, regelmäßig (pro Woche/Monat/Jahr), Stunden, Tageszeit, Wochentage, Flexibel, WhatsApp-Zustimmung
4. **Fähigkeiten/Anmerkungen** (textarea, optional)
5. **Strafrechtliche Unbescholtenheit** (boolean, required) — "Ich erkläre, dass gegen mich keinerlei strafgerichtliche Verurteilungen... bestehen"
6. **Einwilligung zur Datenverarbeitung** (boolean, required) — GDPR consent per BR-ORG-FORM-05

### API endpoints

| Method & Route | Purpose |
|---|---|
| `POST /api/v1/organizations/{id}/forms` | Create intake form (Coordinator/Admin) |
| `GET /api/v1/organizations/{id}/forms/{type}` | Get active form for type (volunteer/help_seeker) |
| `PUT /api/v1/organizations/{id}/forms/{formId}/fields` | Update fields (mandatory/optional toggles) |
| `POST /api/v1/organizations/{id}/forms/{formId}/submissions` | Submit intake form (applicant) |
| `GET /api/v1/organizations/{id}/forms/{formId}/submissions` | List submissions (Coordinator) |
| `PUT /api/v1/organizations/{id}/forms/{formId}/submissions/{subId}:decide` | Approve/Decline (Coordinator) |
| `POST /api/v1/organizations/{id}/forms/{formId}:activate-template` | Activate FWZ Innsbruck-Land template |

### Authorization
- Form CRUD: `OrganizationAdmin` or `Coordinator` of that organization (via `IOrganizationCoordinatorReader`)
- Submit: any authenticated user
- Review: `OrganizationAdmin` or `Coordinator` of that organization

### Seed
- `P2-39`: Activate FWZ Innsbruck-Land template via endpoint or migration

## Flutter Phase 1: Auth & Profile Screens

### P1-27: Multi-Provider Auth Screen (`lib/features/auth/auth_screen.dart`)

**UI:**
- Prominent **Google Sign-In** button (one-tap)
- Prominent **ID Austria** button (one-tap)  
- **Email + Password** tab:
  - Email field
  - Password field + Confirm Password field (real-time match validation)
  - Helper text: "Verification email will be sent"
- **Phone OTP** fallback (existing SMS flow)
- All three locales (de/en/fa), RTL for fa, dark/light, text scaling 1.0/1.5/2.0
- Senior Mode: large touch targets (64dp), increased font

**Flow:**
1. Google → OAuth redirect → `POST /auth/google` → tokens
2. ID Austria → OIDC redirect → `POST /auth/id-austria` → tokens  
3. Email+Password → `POST /auth/register` → email sent → user clicks link → `GET /auth/verify-email?token=` → `POST /auth/login` → tokens
4. Phone → existing OTP flow

### P1-28: Profile & Austrian Address Screen (`lib/features/profile/profile_screen.dart`)

**Sections:**
1. **Basic**: First name, Last name, Profile photo (picker), Interests/Themen (multi-select chips)
2. **Mobile Phone**: Text field + "Verify Phone Number" button → opens SMS OTP dialog → calls `POST /me/phone/request-verification` → `POST /me/phone/verify` → shows checkmark when `phone_verified_at_utc` set
3. **Austrian Address**: Cascading dropdowns (Bundesland → Bezirk → Gemeinde → PLZ/Ort) via `/reference/austria/*` + "Lookup Address on Map" button:
   - Calls `POST /reference/geocode-address` with typed address
   - Shows interactive map preview (mapbox/flutter_map) with pin
   - User confirms → persists lat/lng + `austrian_gemeinde_code` to profile
4. **Accessibility**: Semantic labels, 48/64dp targets, TalkBack/VoiceOver tested

### State Management
- Cubit + Freezed for auth/profile state
- go_router guards read capabilities from server (existing pattern)
- easy_localization keys in all three locales

## Test Plan

### Backend
- Unit: form field validation (required vs optional), FWZ template seed
- Integration: form CRUD, submission, approval flow; cross-org isolation
- Negative: non-coordinator cannot CRUD forms; 401/403/404 on all endpoints

### Flutter
- Widget tests: auth screen renders all 3 providers; password confirm validation
- Integration: auth flow (mock HTTP), profile form submission, address cascade
- A11y: text scale 2.0 no overflow; RTL mirroring; semantic labels

## Build Order

1. **Backend Intake Forms Domain** → migration
2. **Backend Intake Forms Application/Infrastructure** + endpoints
3. **FWZ template seed** (migration or activation endpoint)
4. **Flutter Auth Screen** (`P1-27`)
5. **Flutter Profile Screen** (`P1-28`)