# Data Model

> Conventions: `id uuid` (v7, time-ordered) primary keys · `snake_case` tables and columns ·
> UTC timestamps named `*_at_utc` · every important table carries
> `created_at_utc, created_by, updated_at_utc, updated_by` · `xmin` used as the EF
> concurrency token · organization-scoped tables carry a **nullable** `organization_id`.

---

## 1. Identity & Profiles

```
users
  id · email · email_verified_at_utc · phone · phone_verified_at_utc
  password_hash · display_name · date_of_birth · preferred_locale
  status (Active|Suspended|Deactivated|Deleted)
  senior_mode_default bool
  created_at_utc · … · is_deleted

user_capabilities            -- what the user may do, computed + granted
  id · user_id · capability (enum) · granted_by_user_id · granted_by_org_id
  reason · granted_at_utc · expires_at_utc

senior_profiles
  id · user_id (unique) · address_line · postal_code · city · country
  latitude · longitude                  -- PostGIS-ready
  mobility_note        -- FUNCTIONAL only, never a diagnosis (BR-GDPR-02)
  living_situation · preferred_contact_method
  vulnerability_flag bool               -- set only by staff, drives Safety Level 5
  vulnerability_set_by_user_id · vulnerability_reason

volunteer_profiles
  id · user_id (unique) · bio · max_distance_km · max_activities_per_week
  has_car bool · postal_code · latitude · longitude
  reliability_score numeric             -- computed, never user-editable
  active_since_utc · is_accepting_requests bool

interests            id · code · name_key
user_interests       user_id · interest_id
languages            id · iso_code
user_languages       user_id · language_id · proficiency
skills               id · code · name_key · requires_verification bool
volunteer_skills     volunteer_profile_id · skill_id · verified_at_utc · verified_by_org_id

availability_slots
  id · user_id · day_of_week · start_time · end_time · valid_from · valid_until
```

**Why separate profile tables:** a single `users` table with 60 nullable columns becomes
unmaintainable, and a user can legitimately be both a senior and a volunteer (BR: personas
are not exclusive).

---

## 2. Family

```
family_relationships
  id · senior_user_id · family_user_id · relationship_type
  status (Invited|Active|Revoked) · invited_at_utc · confirmed_at_utc
  confirmed_by_user_id · revoked_at_utc
  UNIQUE(senior_user_id, family_user_id)

family_permissions
  id · family_relationship_id · permission (enum) · granted bool
  granted_by_user_id · granted_at_utc · revoked_at_utc

trusted_contacts
  id · senior_user_id · contact_user_id (nullable) · name · phone
  is_emergency_contact bool · priority
  notify_on_checkin · notify_on_checkout · notify_on_missed
  notify_on_new_assignment · notify_on_safety_alert
```

---

## 3. Community

```
community_groups
  id · organization_id (NULLABLE) · scope (Platform|Community|Organization|Private)
  created_by_user_id · title · description · category
  location_name · latitude · longitude · postal_code
  join_policy (Open|Approval|InviteOnly) · capacity
  status (Active|Paused|Archived) · is_deleted

group_members
  id · group_id · user_id · role (Organizer|Member)
  status (Pending|Active|Left|Removed) · joined_at_utc

group_interests        group_id · interest_id

events
  id · group_id (nullable) · organization_id (nullable) · created_by_user_id
  title · description · category
  starts_at_utc · ends_at_utc · timezone
  recurrence_rule            -- RFC 5545 RRULE subset; null = one-off
  parent_event_id            -- occurrence → series
  location_name · address · latitude · longitude
  capacity · status (Planned|Confirmed|Cancelled|Completed)
  cancellation_reason

event_registrations
  id · event_id · user_id · status (Registered|Waitlisted|Cancelled|Attended|NoShow)
  registered_at_utc · registered_by_user_id     -- on-behalf-of support
  UNIQUE(event_id, user_id)
```

**Recurrence:** store the series as one `events` row with an `RRULE`, and materialise
occurrences as child rows **only when something happens to them** (a registration, a
cancellation, a change). Never expand a year of occurrences into the database eagerly.

---

## 4. Help & Matching

```
help_request_categories
  id · code · name_key · description_key
  default_safety_level · is_blocked bool · referral_group   -- BR-SCOPE-02
  is_active

help_requests
  id · organization_id (nullable) · subject_user_id · created_by_user_id
  category_id · title · description
  requested_start_utc · requested_end_utc · is_flexible_timing
  location_type (SeniorHome|PublicPlace|Institution|Other)
  address · latitude · longitude · postal_code
  required_safety_level        -- SERVER-COMPUTED, never from client
  required_trust_level         -- SERVER-COMPUTED
  insurance_context (OrganizationCovered|PrivateNeighbourly|Unknown)
  status (Draft|Open|Matching|Offered|Assigned|InProgress|Completed|Cancelled|Expired|NoShow)
  assigned_volunteer_id (nullable)
  cancellation_reason_code · cancellation_note
  xmin                          -- concurrency token

help_request_status_history
  id · help_request_id · from_status · to_status
  changed_by_user_id · reason · changed_at_utc

help_offers
  id · help_request_id · volunteer_user_id
  status (Proposed|Offered|Accepted|Declined|Withdrawn|Expired)
  match_score numeric · score_breakdown jsonb    -- explainability
  offered_at_utc · responded_at_utc · expires_at_utc

activity_checkins
  id · help_request_id · volunteer_user_id
  checked_in_at_utc · checkin_latitude · checkin_longitude · checkin_accuracy_m
  checked_out_at_utc · checkout_latitude · checkout_longitude
  duration_minutes           -- computed on checkout
  location_verified bool

volunteer_hours
  id · volunteer_user_id · organization_id (nullable)
  source_type (HelpRequest|Event|Manual) · source_id
  minutes · occurred_on · confirmed_by_user_id · confirmed_at_utc

activity_feedback
  id · source_type · source_id · from_user_id · about_user_id
  sentiment (Good|Neutral|Poor) · note
  is_visible_to_subject bool           -- default false
  created_at_utc
```

**Note on `reports`:** there is deliberately **no** polymorphic `reports` table.
Ordinary feedback lives in `activity_feedback`. Concerns live in the safeguarding schema.
Mixing "the event was cancelled" with "I suspect neglect" in one table is the single most
dangerous design mistake available here.

---

## 5. Trust & Safety

```
verifications
  id · user_id
  type (Email|Phone|Identity|Address|Organization|Training|BackgroundCheck)
  status (NotStarted|Pending|Submitted|Verified|Rejected|Expired)
  provider (Manual|Organization|IdAustria|Kyc)
  verified_by_user_id · verified_by_organization_id
  verified_at_utc · valid_until_utc
  external_reference          -- provider's ID; NEVER the document itself
  rejection_reason
  -- NO document blob, NO document number, NO scan.  BR-TRUST-05

trainings              id · code · name_key · required_for_safety_level · valid_months
user_trainings         id · user_id · training_id · completed_at_utc · expires_at_utc
                       verified_by_organization_id

trust_level_snapshots  -- audit trail of computed levels
  id · user_id · level · computed_at_utc · reason jsonb

user_blocks
  id · blocker_user_id · blocked_user_id · reason · created_at_utc
```

## 6. Safeguarding — *separate schema `safeguarding`, separate access policy*

```
safeguarding.cases
  id · organization_id (nullable) · subject_user_id · reported_by_user_id
  category · severity (Low|Medium|High|Critical)
  description
  status (Open|UnderReview|ActionTaken|Resolved|Closed)
  assigned_to_user_id · created_at_utc · resolved_at_utc · resolution_summary
  xmin

safeguarding.case_notes
  id · case_id · author_user_id · note · created_at_utc     -- append-only

safeguarding.case_actions
  id · case_id · action_type · description · performed_by_user_id · performed_at_utc

safeguarding.case_access_log
  id · case_id · user_id · action (View|Edit|Assign|Resolve) · at_utc · ip_hash
```

Separate PostgreSQL schema so that a mistaken `SELECT *` in a report query cannot reach it,
and so DB-level grants can be applied independently.

---

## 7. Organizations

```
organizations
  id · name · legal_name · type (NGO|Municipality|Company|Association)
  status · support_email · support_phone
  branding jsonb            -- logo_url, primary_color, display_name
  created_at_utc

organization_branches
  id · organization_id · name · address · postal_code · city
  latitude · longitude

organization_memberships
  id · organization_id · branch_id (nullable) · user_id
  role (Staff|Coordinator|Admin|SafeguardingOfficer|Volunteer|Client)
  status (Invited|Active|Suspended|Left) · joined_at_utc
  -- Note: SafeguardingOfficer is a SEPARATE role, never implied by Admin

organization_policies
  id · organization_id · policy_key · policy_value jsonb
  -- e.g. min trust level per safety level, buddy rule on/off, matching weights
```

---

## 8. Audit, Consent, Notifications

```
audit_entries                      -- APPEND ONLY, no update/delete grant
  id · actor_user_id · actor_organization_id
  action · subject_type · subject_id · reason
  metadata jsonb · correlation_id · at_utc · ip_hash

consents
  id · user_id · consent_type · document_version
  granted bool · granted_at_utc · withdrawn_at_utc · ip_hash

notifications
  id · user_id · type · title_key · body_key · payload jsonb
  channel (Push|Sms|Email|InApp) · status · sent_at_utc · read_at_utc

notification_preferences
  id · user_id · category · push bool · sms bool · email bool
  quiet_hours_start · quiet_hours_end
```

---

## 9. Indexing (get these right from the first migration)

```sql
CREATE INDEX ix_help_requests_open_geo
  ON help_requests (status, requested_start_utc)
  WHERE status IN ('open','matching');

CREATE INDEX ix_help_requests_subject   ON help_requests (subject_user_id, status);
CREATE INDEX ix_help_requests_org       ON help_requests (organization_id, status);
CREATE INDEX ix_events_upcoming         ON events (starts_at_utc) WHERE status <> 'cancelled';
CREATE INDEX ix_group_members_user      ON group_members (user_id, status);
CREATE INDEX ix_verifications_user_type ON verifications (user_id, type, status);
CREATE INDEX ix_verifications_expiring  ON verifications (valid_until_utc)
                                        WHERE status = 'verified';
CREATE INDEX ix_audit_subject           ON audit_entries (subject_type, subject_id, at_utc DESC);
CREATE INDEX ix_audit_actor             ON audit_entries (actor_user_id, at_utc DESC);

-- Phase 8, after CREATE EXTENSION postgis:
-- ALTER TABLE senior_profiles ADD COLUMN geo geography(Point,4326)
--   GENERATED ALWAYS AS (ST_MakePoint(longitude, latitude)::geography) STORED;
-- CREATE INDEX ix_senior_geo ON senior_profiles USING GIST (geo);
```

---

## 10. Entity relationship summary

```
users ─┬─ senior_profiles
       ├─ volunteer_profiles
       ├─ user_capabilities
       ├─ verifications ── trust_level_snapshots
       ├─ family_relationships ── family_permissions
       ├─ organization_memberships ── organizations ── organization_branches
       ├─ group_members ── community_groups ── events ── event_registrations
       ├─ help_requests (as subject / creator / assignee)
       │     ├─ help_offers
       │     ├─ help_request_status_history
       │     ├─ activity_checkins ── volunteer_hours
       │     └─ activity_feedback
       └─ safeguarding.cases (subject / reporter / assignee)   ← restricted
```

---

# Additions from discovery (v2)

> Source: `docs/product/discovery-findings.md` and ADR-015/016/017.
> These tables move **earlier** than the original phase plan — `activities`,
> `funders` and the insurance context are Phase 2, not Phase 6.

## 11. Activity — the standalone record (Phase 2)

The single most important addition. A coordinator must be able to record
"Anna visited Frau Müller on Tuesday for 90 minutes" **before the help-request
workflow exists**. In Phase 3, a completed `help_request` produces an `activity`;
the model does not fork.

```
activities
  id · organization_id (NULLABLE) · branch_id (nullable)
  volunteer_user_id · subject_user_id (nullable — group activities have none)
  help_request_id (nullable)         -- Phase 3 link; null for directly logged work
  event_id (nullable)                -- Phase 5 link
  category_id
  occurred_on · started_at_utc · ended_at_utc · duration_minutes
  location_type · notes

  insurance_context   -- OrganizationCovered | PrivateNeighbourly | Unknown   (F4)
  insurance_disclaimer_accepted_at_utc
  insurance_disclaimer_accepted_by_user_id

  involves_transport bool                                                    (F4)
  transport_mode      -- PublicTransportTogether | VolunteerPrivateVehicle |
                      --  OrganizationVehicle | Taxi | None
  transport_distance_km

  source        -- SelfLogged | CoordinatorLogged | FromHelpRequest | FromEvent
  logged_by_user_id · logged_at_utc
  confirmed_by_user_id · confirmed_at_utc
  status        -- Draft | Logged | Confirmed | Disputed | Cancelled
  xmin
```

Constraints worth writing into the migration:

```sql
-- BR-TRANSPORT-04: a private-vehicle activity cannot be confirmed with
-- an unresolved insurance context. Enforced in the domain AND here.
CONSTRAINT ck_activities_transport_insurance CHECK (
    transport_mode <> 'volunteer_private_vehicle'
    OR status <> 'confirmed'
    OR insurance_context <> 'unknown'
)

CONSTRAINT ck_activities_duration CHECK (duration_minutes BETWEEN 1 AND 1440)
```

`volunteer_hours` from §4 becomes a **view** over confirmed activities rather than
a separate table. One source of truth for the number the whole product is sold on.

```sql
CREATE VIEW v_volunteer_hours AS
SELECT volunteer_user_id, organization_id, occurred_on,
       SUM(duration_minutes) AS minutes
FROM activities
WHERE status = 'confirmed'
GROUP BY volunteer_user_id, organization_id, occurred_on;
```

Indexes:

```sql
CREATE INDEX ix_activities_org_month
  ON activities (organization_id, occurred_on) WHERE status = 'confirmed';
CREATE INDEX ix_activities_volunteer
  ON activities (volunteer_user_id, occurred_on DESC);
CREATE INDEX ix_activities_unconfirmed
  ON activities (organization_id, logged_at_utc) WHERE status = 'logged';
CREATE INDEX ix_activities_insurance_unresolved
  ON activities (organization_id) WHERE insurance_context = 'unknown';
```

## 12. Roster status (Phase 2) — *F2*

Derived, never stored as a manually editable column. Materialise it nightly for
dashboard speed, but always from behaviour:

```sql
CREATE MATERIALIZED VIEW mv_volunteer_roster_status AS
SELECT vp.user_id,
       om.organization_id,
       MAX(a.occurred_on) AS last_activity_on,
       CASE
         WHEN MAX(a.occurred_on) IS NULL                       THEN 'never_activated'
         WHEN MAX(a.occurred_on) >= CURRENT_DATE - 60          THEN 'active'
         WHEN MAX(a.occurred_on) >= CURRENT_DATE - 120         THEN 'dormant'
         ELSE 'inactive'
       END AS roster_status
FROM volunteer_profiles vp
JOIN organization_memberships om ON om.user_id = vp.user_id
LEFT JOIN activities a
       ON a.volunteer_user_id = vp.user_id
      AND a.status = 'confirmed'
GROUP BY vp.user_id, om.organization_id;
```

Thresholds are configuration (BR-ROSTER-02) — parameterise them rather than
hardcoding 60/120 in the view when you build it for real.

## 13. Onboarding pipeline (Phase 2) — *F10*

```
volunteer_applications
  id · organization_id · user_id
  status · applied_at_utc · decided_at_utc · decided_by_user_id · decline_reason

volunteer_application_steps
  id · application_id · step        -- Interview | BackgroundCheck |
                                    --  ConfidentialityAgreement | Briefing | Approval
  status        -- NotStarted | InProgress | Completed | Blocked
  opened_at_utc · completed_at_utc · sla_days · note
  -- days_open is computed, not stored; it drives the coordinator's task list
```

## 14. Key custody (Phase 4) — *F5*

```
key_custody
  id · organization_id (nullable) · subject_user_id · holder_user_id
  description                 -- "Wohnungstür, 1 Schlüssel, gelber Anhänger"
  handed_over_at_utc · handed_over_by_user_id
  subject_confirmed_at_utc    -- BR-KEYS-02, required
  returned_at_utc · received_by_user_id
  status                      -- held | returned | reported_lost
  reported_lost_at_utc · lost_note
```

**No key codes, no photographs of keys, no lock or alarm information** (BR-KEYS-05).

```sql
CREATE INDEX ix_key_custody_open ON key_custody (organization_id, holder_user_id)
  WHERE status = 'held';
```

## 15. Expense records (Phase 4) — *F6*

```
activity_expenses
  id · activity_id (unique) · currency
  amount_given · amount_spent · amount_returned
  discrepancy_note                       -- required when the arithmetic disagrees
  receipt_image_id (nullable)            -- encrypted object storage
  confirmed_by_subject_at_utc
  confirmed_by_volunteer_at_utc
  status                                 -- draft | confirmed | disputed
  disputed_by_user_id · disputed_at_utc · dispute_note
```

```sql
CONSTRAINT ck_activity_expenses_balance CHECK (
    amount_given - amount_spent = amount_returned
    OR discrepancy_note IS NOT NULL
)
```

No money moves through the platform. This is a record, not a transaction.

## 16. Funders (Phase 2) — *F7, ADR-017*

```
funders
  id · name · type            -- Municipality | Foundation | PublicBody | Corporate
  contact_email · contact_phone · status · created_at_utc

funding_relationships
  id · funder_id · organization_id
  valid_from · valid_until
  reporting_scope jsonb       -- { metrics: [...], geography: [...], programme: "..." }
  created_at_utc · created_by

funder_memberships
  id · funder_id · user_id · role     -- Viewer | Admin
  status · joined_at_utc
```

There is deliberately **no** foreign key from `funders` to any user-level table.
A funder cannot reach an individual through the schema, let alone through the API.

## 17. Driving licence verification — *F4*

Add to the `verifications.type` check constraint:

```
'driving_licence'
```

Required by BR-TRANSPORT-03 before `transport_mode = VolunteerPrivateVehicle` may
be used. Like every other verification: the outcome is stored, the document is not.

## 18. Multi-Provider Authentication & Verification (Phase 1) — *F3, ADR-016 & ADR-021*

`users.password_hash` is **nullable**. Authentication supports Google OAuth, ID Austria eIDAS, verified Email+Password, and Phone SMS OTP.

```
user_external_logins
  id · user_id · provider (google | id_austria) · provider_key
  email · display_name · linked_at_utc

email_verification_tokens
  id · user_id · token_hash · expires_at_utc · used_at_utc · created_at_utc

otp_challenges
  id · user_id (nullable — may precede account creation)
  channel            -- sms | email
  destination_hash   -- hashed phone or email, never plaintext in this table
  code_hash
  purpose            -- login | phone_verification | phone_change | recovery
  attempts smallint · max_attempts smallint DEFAULT 5
  created_at_utc · expires_at_utc  DEFAULT now() + interval '5 minutes'
  consumed_at_utc · ip_hash

CREATE INDEX ix_otp_active ON otp_challenges (destination_hash, created_at_utc DESC)
  WHERE consumed_at_utc IS NULL;
```

Also in `users`:

```
primary_auth_method   -- google | id_austria | email_password | phone_otp
phone_verified_at_utc -- set via explicit profile verification action
email_verified_at_utc -- set via email confirmation link
```

## 19. Notification budget (Phase 7) — *F3*

```
notification_ledger
  id · user_id · category · channel
  sent_at_utc · budget_bucket        -- e.g. 'week:2026-W34' or 'assignment:<uuid>'
  was_downgraded bool                -- true when the budget forced in-app only
```

The ledger is what makes BR-NOTIFY testable: a simulated week of activity is
replayed and the ledger is asserted against the budget.

---

## 20. Austrian Administrative Hierarchy & Geocoding (Phase 1 & 2) — *ADR-021, BR-GEO*

```
austrian_administrative_units
  id · bundesland_code (e.g. '7' for Tirol) · bundesland_name ('Tirol')
  bezirk_code ('703') · bezirk_name ('Innsbruck-Land')
  gemeinde_code ('70320') · gemeinde_name ('Kematen in Tirol')
  postal_code ('6175') · locality_name ('Kematen in Tirol')
  latitude numeric(9,6) · longitude numeric(9,6)
  is_active bool DEFAULT true

CREATE INDEX ix_austria_geo_plz ON austrian_administrative_units (postal_code);
CREATE INDEX ix_austria_geo_gemeinde ON austrian_administrative_units (gemeinde_name);
CREATE INDEX ix_austria_geo_coords ON austrian_administrative_units (latitude, longitude);
```

Added to `senior_profiles` and `volunteer_profiles`:
```
address_verified bool DEFAULT false
geocoded_at_utc timestamptz
austrian_gemeinde_code varchar(10) (FK to austrian_administrative_units)
```

---

## 21. Dynamic Organization Intake Forms (Phase 2) — *ADR-021, BR-ORG-FORM*

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
  is_required bool DEFAULT false   -- coordinator controls mandatory vs optional
  options_json jsonb               -- e.g. categories, target groups, time slots
  sort_order int

intake_form_submissions
  id · form_id · organization_id · user_id
  status (draft | submitted | approved | declined)
  submission_data_json jsonb       -- answers structured by field_key
  criminal_clearance_declared bool -- Strafrechtliche Unbescholtenheit confirmation
  criminal_clearance_declared_at_utc timestamptz
  gdpr_consent_accepted bool       -- Einwilligung zur Datenverarbeitung confirmation
  gdpr_consent_accepted_at_utc timestamptz
  event_invitation_opt_in bool
  submitted_at_utc · decided_at_utc · decided_by_user_id · review_notes
```

