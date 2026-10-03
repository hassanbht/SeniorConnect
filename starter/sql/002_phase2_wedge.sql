-- =============================================================================
-- SeniorConnect — Phase 2 schema: the Coordinator Wedge
--
-- Depends on 001_phase1_init.sql
--
-- This phase exists to replace the coordinator's Excel file. Everything here
-- serves one of five findings:
--   F1  hours are retyped by hand and December costs two weeks
--   F2  60 on the roster, 22 actually active, tracked from memory
--   F4  an insurance dispute has already happened
--   F7  the Gemeinde is a funder and must never see a name
--   F10 onboarding takes 2-4 weeks and nobody tells the applicant
--
-- Reference schema. In practice you generate this with EF Core migrations —
-- use it to review what the migration SHOULD produce.
-- =============================================================================

BEGIN;

-- =============================================================================
-- 1. Organizations
-- =============================================================================

CREATE TABLE organizations (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    name                text        NOT NULL,
    legal_name          text,
    -- NOTE: 'municipality' is deliberately ABSENT. A Gemeinde is a funder,
    -- not an organization. See ADR-017.
    type                text        NOT NULL,
    status              text        NOT NULL DEFAULT 'active',
    support_email       text,
    support_phone       text,
    branding            jsonb       NOT NULL DEFAULT '{}'::jsonb,

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,
    is_deleted          boolean     NOT NULL DEFAULT false,

    CONSTRAINT ck_organizations_type
        CHECK (type IN ('ngo','association','parish','company','other')),
    CONSTRAINT ck_organizations_status
        CHECK (status IN ('active','suspended','archived'))
);

CREATE TABLE organization_branches (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    organization_id     uuid        NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name                text        NOT NULL,
    address             text,
    postal_code         text,
    city                text,
    latitude            double precision,
    longitude           double precision,
    is_active           boolean     NOT NULL DEFAULT true,

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,

    CONSTRAINT ck_branches_geo_pair CHECK ((latitude IS NULL) = (longitude IS NULL))
);

CREATE INDEX ix_branches_org ON organization_branches (organization_id) WHERE is_active;

CREATE TABLE organization_memberships (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    organization_id     uuid        NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    branch_id           uuid        REFERENCES organization_branches(id),
    user_id             uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    -- safeguarding_officer is a SEPARATE role. admin does NOT imply it. BR-SG-02
    role                text        NOT NULL,
    status              text        NOT NULL DEFAULT 'invited',
    joined_at_utc       timestamptz,
    left_at_utc         timestamptz,

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,

    CONSTRAINT ck_memberships_role CHECK (role IN
        ('staff','coordinator','admin','safeguarding_officer','volunteer','client')),
    CONSTRAINT ck_memberships_status CHECK (status IN
        ('invited','active','suspended','left'))
);

CREATE UNIQUE INDEX ux_memberships_org_user_role
    ON organization_memberships (organization_id, user_id, role)
    WHERE status <> 'left';
CREATE INDEX ix_memberships_user ON organization_memberships (user_id, status);
CREATE INDEX ix_memberships_org_role
    ON organization_memberships (organization_id, role) WHERE status = 'active';

CREATE TABLE organization_policies (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    organization_id     uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    policy_key          text NOT NULL,
    policy_value        jsonb NOT NULL,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,

    UNIQUE (organization_id, policy_key)
);

-- Roster thresholds live here, not in code. BR-ROSTER-02.
COMMENT ON TABLE organization_policies IS
  'Per-org configuration: roster_dormant_days, roster_inactive_days, '
  'matching weights, buddy_rule_enabled, onboarding SLA days.';

-- Now that organizations exist, close the forward references left open in 001.
ALTER TABLE user_capabilities
    ADD CONSTRAINT fk_user_capabilities_org
    FOREIGN KEY (granted_by_org_id) REFERENCES organizations(id);

ALTER TABLE verifications
    ADD CONSTRAINT fk_verifications_org
    FOREIGN KEY (verified_by_organization_id) REFERENCES organizations(id);

ALTER TABLE volunteer_skills
    ADD CONSTRAINT fk_volunteer_skills_org
    FOREIGN KEY (verified_by_org_id) REFERENCES organizations(id);

-- Driving licence becomes a verification type. BR-TRANSPORT-03.
ALTER TABLE verifications DROP CONSTRAINT ck_verifications_type;
ALTER TABLE verifications ADD CONSTRAINT ck_verifications_type CHECK (type IN
    ('email','phone','identity','address','organization','training',
     'background_check','driving_licence'));

-- =============================================================================
-- 2. Activity categories
--
-- Shared by Phase 2 (directly logged activities) and Phase 3 (help requests).
-- Defining them once here prevents two parallel taxonomies later.
-- =============================================================================

CREATE TABLE activity_categories (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    code                    text        NOT NULL UNIQUE,
    name_key                text        NOT NULL,
    description_key         text,
    default_safety_level    smallint    NOT NULL DEFAULT 1,
    -- BR-SCOPE-02: blocked categories produce a REFERRAL, never a request
    is_blocked              boolean     NOT NULL DEFAULT false,
    referral_group          text,
    typically_involves_transport boolean NOT NULL DEFAULT false,
    typically_involves_money     boolean NOT NULL DEFAULT false,
    sort_order              smallint    NOT NULL DEFAULT 100,
    is_active               boolean     NOT NULL DEFAULT true,

    CONSTRAINT ck_activity_categories_safety
        CHECK (default_safety_level BETWEEN 1 AND 5),
    CONSTRAINT ck_activity_categories_blocked_needs_referral
        CHECK (NOT is_blocked OR referral_group IS NOT NULL)
);

-- =============================================================================
-- 3. Activities — the heart of Phase 2
--
-- A standalone record. A coordinator must be able to log "Anna visited Frau
-- Müller on Tuesday for 90 minutes" BEFORE the help-request workflow exists.
-- In Phase 3 a completed help_request produces an activity; the model does not
-- fork into two parallel histories.
-- =============================================================================

CREATE TABLE activities (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- NULLABLE by design. ADR-007: the platform works without organizations.
    organization_id         uuid        REFERENCES organizations(id),
    branch_id               uuid        REFERENCES organization_branches(id),

    volunteer_user_id       uuid        NOT NULL REFERENCES users(id),
    subject_user_id         uuid        REFERENCES users(id),   -- null for group work
    category_id             uuid        NOT NULL REFERENCES activity_categories(id),

    help_request_id         uuid,       -- Phase 3, FK added there
    event_id                uuid,       -- Phase 5, FK added there

    occurred_on             date        NOT NULL,
    started_at_utc          timestamptz,
    ended_at_utc            timestamptz,
    duration_minutes        integer     NOT NULL,

    location_type           text        NOT NULL DEFAULT 'other',
    -- Operational instruction only. Never a diagnosis. BR-VISIBILITY-01.
    notes                   text,

    -- ---- Insurance (F4) -----------------------------------------------------
    insurance_context       text        NOT NULL DEFAULT 'unknown',
    insurance_disclaimer_accepted_at_utc      timestamptz,
    insurance_disclaimer_accepted_by_user_id  uuid REFERENCES users(id),

    -- ---- Transport (BR-TRANSPORT) ------------------------------------------
    involves_transport      boolean     NOT NULL DEFAULT false,
    transport_mode          text        NOT NULL DEFAULT 'none',
    transport_distance_km   numeric(6,1),

    -- ---- Provenance ---------------------------------------------------------
    source                  text        NOT NULL,
    logged_by_user_id       uuid        NOT NULL REFERENCES users(id),
    logged_at_utc           timestamptz NOT NULL DEFAULT now(),
    confirmed_by_user_id    uuid        REFERENCES users(id),
    confirmed_at_utc        timestamptz,

    status                  text        NOT NULL DEFAULT 'logged',
    cancellation_reason     text,

    created_at_utc          timestamptz NOT NULL DEFAULT now(),
    created_by              uuid,
    updated_at_utc          timestamptz NOT NULL DEFAULT now(),
    updated_by              uuid,

    CONSTRAINT ck_activities_status CHECK (status IN
        ('draft','logged','confirmed','disputed','cancelled')),
    CONSTRAINT ck_activities_source CHECK (source IN
        ('self_logged','coordinator_logged','from_help_request','from_event')),
    CONSTRAINT ck_activities_location CHECK (location_type IN
        ('senior_home','public_place','institution','organization','other')),
    CONSTRAINT ck_activities_insurance CHECK (insurance_context IN
        ('organization_covered','private_neighbourly','unknown')),
    CONSTRAINT ck_activities_transport_mode CHECK (transport_mode IN
        ('none','public_transport_together','volunteer_private_vehicle',
         'organization_vehicle','taxi')),

    CONSTRAINT ck_activities_duration CHECK (duration_minutes BETWEEN 1 AND 1440),
    CONSTRAINT ck_activities_time_order
        CHECK (started_at_utc IS NULL OR ended_at_utc IS NULL
               OR started_at_utc < ended_at_utc),
    -- NOTE: "occurred_on must not be in the future" is deliberately NOT a CHECK
    -- constraint. Postgres accepts CURRENT_DATE here but re-evaluates it on
    -- every UPDATE and during pg_restore, so a row valid when written can later
    -- block an unrelated update. Enforce it in the domain layer
    -- (LogActivityCommand validator) instead.


    CONSTRAINT ck_activities_transport_consistency
        CHECK (involves_transport = (transport_mode <> 'none')),

    -- BR-TRANSPORT-04. A private-vehicle activity cannot be CONFIRMED while the
    -- insurance context is unknown. Also enforced in the domain layer — this is
    -- the backstop, not the primary guard.
    CONSTRAINT ck_activities_transport_insurance CHECK (
        transport_mode <> 'volunteer_private_vehicle'
        OR status <> 'confirmed'
        OR insurance_context <> 'unknown'
    ),

    -- If someone accepted a disclaimer, record who and when, or neither.
    CONSTRAINT ck_activities_disclaimer_pair CHECK (
        (insurance_disclaimer_accepted_at_utc IS NULL)
        = (insurance_disclaimer_accepted_by_user_id IS NULL)
    ),

    CONSTRAINT ck_activities_confirmed_pair CHECK (
        (confirmed_at_utc IS NULL) = (confirmed_by_user_id IS NULL)
    ),
    CONSTRAINT ck_activities_confirmed_status CHECK (
        status <> 'confirmed' OR confirmed_at_utc IS NOT NULL
    ),

    -- A volunteer cannot log an activity for themselves as the subject.
    CONSTRAINT ck_activities_distinct_parties
        CHECK (subject_user_id IS NULL OR subject_user_id <> volunteer_user_id)
);

CREATE INDEX ix_activities_org_month
    ON activities (organization_id, occurred_on) WHERE status = 'confirmed';
CREATE INDEX ix_activities_volunteer
    ON activities (volunteer_user_id, occurred_on DESC);
CREATE INDEX ix_activities_subject
    ON activities (subject_user_id, occurred_on DESC) WHERE subject_user_id IS NOT NULL;
CREATE INDEX ix_activities_unconfirmed
    ON activities (organization_id, logged_at_utc) WHERE status = 'logged';
CREATE INDEX ix_activities_insurance_unresolved
    ON activities (organization_id) WHERE insurance_context = 'unknown'
                                      AND status IN ('logged','confirmed');
CREATE INDEX ix_activities_category
    ON activities (category_id, occurred_on);

CREATE TRIGGER tr_activities_updated BEFORE UPDATE ON activities
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Volunteer hours are a VIEW, not a table. One source of truth for the number
-- the entire product is sold on. F1.
CREATE VIEW v_volunteer_hours AS
SELECT a.volunteer_user_id,
       a.organization_id,
       a.occurred_on,
       date_trunc('month', a.occurred_on)::date AS occurred_month,
       COUNT(*)                                  AS activity_count,
       SUM(a.duration_minutes)                   AS minutes,
       ROUND(SUM(a.duration_minutes) / 60.0, 2)  AS hours
FROM activities a
WHERE a.status = 'confirmed'
GROUP BY a.volunteer_user_id, a.organization_id, a.occurred_on;

-- =============================================================================
-- 4. Roster status (F2)
--
-- Derived from behaviour, never a manually editable column. BR-ROSTER-01.
-- Materialised for dashboard speed; refreshed nightly.
--
-- The 60/120 day thresholds are the DEFAULTS. Per-organization overrides live
-- in organization_policies and are applied in the application layer, which
-- reads last_activity_on from this view rather than recomputing it.
-- =============================================================================

CREATE MATERIALIZED VIEW mv_volunteer_roster AS
SELECT om.organization_id,
       om.user_id,
       MAX(a.occurred_on)                       AS last_activity_on,
       COUNT(a.id) FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365) AS activities_12m,
       COALESCE(SUM(a.duration_minutes)
                FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365), 0) AS minutes_12m,
       CASE
         WHEN MAX(a.occurred_on) IS NULL               THEN 'never_activated'
         WHEN MAX(a.occurred_on) >= CURRENT_DATE -  60 THEN 'active'
         WHEN MAX(a.occurred_on) >= CURRENT_DATE - 120 THEN 'dormant'
         ELSE 'inactive'
       END                                      AS roster_status
FROM organization_memberships om
LEFT JOIN activities a
       ON a.volunteer_user_id  = om.user_id
      AND a.organization_id    = om.organization_id
      AND a.status             = 'confirmed'
WHERE om.role   = 'volunteer'
  AND om.status = 'active'
GROUP BY om.organization_id, om.user_id;

CREATE UNIQUE INDEX ux_mv_roster ON mv_volunteer_roster (organization_id, user_id);
CREATE INDEX ix_mv_roster_status ON mv_volunteer_roster (organization_id, roster_status);

-- REFRESH MATERIALIZED VIEW CONCURRENTLY mv_volunteer_roster;   -- nightly job

-- =============================================================================
-- 5. Onboarding pipeline (F10)
--
-- The platform automates NONE of the checks. It makes the wait legible.
-- BR-ONBOARD-03.
-- =============================================================================

CREATE TABLE volunteer_applications (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    organization_id     uuid        NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id             uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    status              text        NOT NULL DEFAULT 'open',
    motivation          text,
    applied_at_utc      timestamptz NOT NULL DEFAULT now(),
    decided_at_utc      timestamptz,
    decided_by_user_id  uuid        REFERENCES users(id),
    decline_reason      text,

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,

    CONSTRAINT ck_applications_status CHECK (status IN
        ('open','approved','declined','withdrawn')),
    CONSTRAINT ck_applications_decline_reason
        CHECK (status <> 'declined' OR decline_reason IS NOT NULL),
    CONSTRAINT ck_applications_decided_pair
        CHECK ((decided_at_utc IS NULL) = (decided_by_user_id IS NULL))
);

CREATE UNIQUE INDEX ux_applications_open
    ON volunteer_applications (organization_id, user_id) WHERE status = 'open';
CREATE INDEX ix_applications_waiting
    ON volunteer_applications (organization_id, applied_at_utc) WHERE status = 'open';

CREATE TABLE volunteer_application_steps (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    application_id      uuid        NOT NULL
                                    REFERENCES volunteer_applications(id) ON DELETE CASCADE,
    step                text        NOT NULL,
    sort_order          smallint    NOT NULL,
    status              text        NOT NULL DEFAULT 'not_started',
    sla_days            smallint    NOT NULL DEFAULT 14,
    opened_at_utc       timestamptz,
    completed_at_utc    timestamptz,
    completed_by_user_id uuid       REFERENCES users(id),
    note                text,

    CONSTRAINT ck_application_steps_step CHECK (step IN
        ('interview','background_check','confidentiality_agreement',
         'briefing','approval')),
    CONSTRAINT ck_application_steps_status CHECK (status IN
        ('not_started','in_progress','completed','blocked','skipped')),
    UNIQUE (application_id, step)
);

-- days_open is COMPUTED, never stored. It drives the coordinator's task list.
CREATE VIEW v_application_steps_open AS
SELECT s.id,
       s.application_id,
       a.organization_id,
       a.user_id,
       s.step,
       s.status,
       s.sla_days,
       s.opened_at_utc,
       (CURRENT_DATE - s.opened_at_utc::date)                 AS days_open,
       (CURRENT_DATE - s.opened_at_utc::date) > s.sla_days    AS is_overdue
FROM volunteer_application_steps s
JOIN volunteer_applications a ON a.id = s.application_id
WHERE s.status IN ('in_progress','blocked')
  AND a.status = 'open'
  AND s.opened_at_utc IS NOT NULL;

-- =============================================================================
-- 6. Funders (F7, ADR-017)
--
-- A funder owns no users, employs no volunteers, runs no activities.
-- There is deliberately NO foreign key from funders to any user-level table.
-- A funder cannot reach an individual through the schema, let alone the API.
-- =============================================================================

CREATE TABLE funders (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    name                text        NOT NULL,
    type                text        NOT NULL,
    contact_email       text,
    contact_phone       text,
    status              text        NOT NULL DEFAULT 'active',

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_by          uuid,

    CONSTRAINT ck_funders_type CHECK (type IN
        ('municipality','foundation','public_body','corporate')),
    CONSTRAINT ck_funders_status CHECK (status IN ('active','suspended','ended'))
);

CREATE TABLE funding_relationships (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    funder_id           uuid        NOT NULL REFERENCES funders(id) ON DELETE CASCADE,
    organization_id     uuid        NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    valid_from          date        NOT NULL,
    valid_until         date,
    -- { "metrics": [...], "geography": ["6020","6010"], "programme": "Besuchsdienst" }
    reporting_scope     jsonb       NOT NULL DEFAULT '{}'::jsonb,

    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          uuid,

    CONSTRAINT ck_funding_dates CHECK (valid_until IS NULL OR valid_until > valid_from)
);

-- NOTE: an index predicate may not use CURRENT_DATE — Postgres requires
-- IMMUTABLE functions there. Index the columns and let the planner filter on
-- valid_until at query time.
CREATE INDEX ix_funding_active
    ON funding_relationships (funder_id, organization_id, valid_from, valid_until);
CREATE INDEX ix_funding_open_ended
    ON funding_relationships (funder_id) WHERE valid_until IS NULL;

CREATE TABLE funder_memberships (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    funder_id           uuid        NOT NULL REFERENCES funders(id) ON DELETE CASCADE,
    user_id             uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role                text        NOT NULL DEFAULT 'viewer',
    status              text        NOT NULL DEFAULT 'invited',
    joined_at_utc       timestamptz,

    CONSTRAINT ck_funder_memberships_role CHECK (role IN ('viewer','admin')),
    CONSTRAINT ck_funder_memberships_status
        CHECK (status IN ('invited','active','suspended','left')),
    UNIQUE (funder_id, user_id)
);

-- -----------------------------------------------------------------------------
-- The funder aggregate view.
--
-- BR-FUNDER-02: no name, no address, no phone, no email, no free text.
-- BR-FUNDER-03: minimum cohort 10 — enforced HERE as well as in the API, so a
-- mistaken query cannot leak a small cell.
--
-- Note there is no volunteer_user_id and no subject_user_id anywhere in this
-- view. Counts are DISTINCT counts, never identifiers.
-- -----------------------------------------------------------------------------

-- INTERNAL ONLY. Contains user identifiers. Never exposed to a funder principal.
-- It exists so the aggregate view below has a single, auditable source.
CREATE VIEW v_activity_facts_internal AS
SELECT a.organization_id,
       date_trunc('month', a.occurred_on)::date AS month,
       c.code                                   AS category_code,
       a.location_type,
       a.transport_mode,
       a.volunteer_user_id,
       a.subject_user_id,
       a.duration_minutes
FROM activities a
JOIN activity_categories c ON c.id = a.category_id
WHERE a.status = 'confirmed';

CREATE VIEW v_funder_monthly_facts AS
SELECT fr.funder_id,
       f.organization_id,
       f.month,
       f.category_code,
       COUNT(*)                                   AS activity_count,
       COUNT(DISTINCT f.volunteer_user_id)        AS distinct_volunteers,
       COUNT(DISTINCT f.subject_user_id)          AS distinct_people_supported,
       ROUND(SUM(f.duration_minutes) / 60.0, 1)   AS hours
FROM v_activity_facts_internal f
JOIN funding_relationships fr
       ON fr.organization_id = f.organization_id
      AND f.month >= date_trunc('month', fr.valid_from)
      AND (fr.valid_until IS NULL OR f.month <= date_trunc('month', fr.valid_until))
GROUP BY fr.funder_id, f.organization_id, f.month, f.category_code;

-- Suppressed presentation view. This is what the funder API reads.
-- Complementary suppression (defeating inference by subtraction) is applied in
-- the application layer, which has the full result set in hand.
CREATE VIEW v_funder_monthly_report AS
SELECT funder_id,
       organization_id,
       month,
       category_code,
       CASE WHEN distinct_people_supported < 10 THEN NULL ELSE activity_count END
           AS activity_count,
       CASE WHEN distinct_people_supported < 10 THEN NULL ELSE distinct_volunteers END
           AS distinct_volunteers,
       CASE WHEN distinct_people_supported < 10 THEN NULL ELSE distinct_people_supported END
           AS distinct_people_supported,
       CASE WHEN distinct_people_supported < 10 THEN NULL ELSE hours END
           AS hours,
       distinct_people_supported < 10 AS is_suppressed
FROM v_funder_monthly_facts;

COMMENT ON VIEW v_funder_monthly_report IS
  'BR-FUNDER-03. A NULL metric with is_suppressed = true renders as "<10" in the '
  'UI. Never render 0 for a suppressed cell — 0 is a fact and invites inference.';

-- =============================================================================
-- 7. Coordinator dashboard: "Braucht heute Aufmerksamkeit"
--
-- The first widget of the first screen of the phase. One view per row of it,
-- unioned in the application layer so each can be indexed independently.
-- =============================================================================

CREATE VIEW v_attention_unconfirmed_hours AS
SELECT organization_id,
       COUNT(*)                              AS item_count,
       MIN(logged_at_utc)                    AS oldest_at_utc
FROM activities
WHERE status = 'logged'
GROUP BY organization_id;

CREATE VIEW v_attention_dormant_volunteers AS
SELECT organization_id,
       COUNT(*) AS item_count
FROM mv_volunteer_roster
WHERE roster_status IN ('dormant','inactive')
GROUP BY organization_id;

CREATE VIEW v_attention_waiting_applications AS
SELECT a.organization_id,
       COUNT(*)                                   AS item_count,
       MAX(CURRENT_DATE - a.applied_at_utc::date) AS longest_wait_days
FROM volunteer_applications a
WHERE a.status = 'open'
  AND CURRENT_DATE - a.applied_at_utc::date > 14
GROUP BY a.organization_id;

CREATE VIEW v_attention_expiring_verifications AS
SELECT v.verified_by_organization_id AS organization_id,
       COUNT(*) AS item_count
FROM verifications v
WHERE v.status = 'verified'
  AND v.valid_until_utc IS NOT NULL
  AND v.valid_until_utc <= now() + interval '30 days'
GROUP BY v.verified_by_organization_id;

CREATE VIEW v_attention_unresolved_insurance AS
SELECT organization_id,
       COUNT(*) AS item_count
FROM activities
WHERE insurance_context = 'unknown'
  AND status IN ('logged','confirmed')
GROUP BY organization_id;

-- =============================================================================
-- 8. Seed: activity categories
--
-- Blocked categories are the legal firewall. BR-SCOPE-02. The referral_group
-- maps to a small, manually curated, per-region directory — curate it for the
-- pilot Gemeinde only. Do not build a national directory.
-- =============================================================================

INSERT INTO activity_categories
    (code, name_key, default_safety_level, is_blocked, referral_group,
     typically_involves_transport, typically_involves_money, sort_order)
VALUES
    -- allowed
    ('shopping',        'help.category.shopping',        2, false, NULL, false, true,   10),
    ('doctor_visit',    'help.category.doctor',          3, false, NULL, true,  false,  20),
    ('authority_visit', 'help.category.authority',       3, false, NULL, true,  false,  30),
    ('accompaniment',   'help.category.accompaniment',   2, false, NULL, false, false,  40),
    ('visit_at_home',   'help.category.visit',           4, false, NULL, false, false,  50),
    ('home_small_help', 'help.category.home_small',      4, false, NULL, false, false,  60),
    ('tech_help',       'help.category.tech',            4, false, NULL, false, false,  70),
    ('garden_help',     'help.category.garden',          2, false, NULL, false, false,  80),
    ('phone_buddy',     'help.category.phone_buddy',     1, false, NULL, false, false,  90),
    ('group_activity',  'help.category.group',           1, false, NULL, false, false, 100),
    ('transport_only',  'help.category.transport',       3, false, NULL, true,  false, 110),
    ('other',           'help.category.other',           2, false, NULL, false, false, 999),

    -- BLOCKED — referral only, never a request. BR-SCOPE-02.
    ('personal_hygiene',  'help.category.blocked.hygiene',    5, true, 'mobile_care',   false, false, 900),
    ('medication_admin',  'help.category.blocked.medication', 5, true, 'mobile_care',   false, false, 901),
    ('wound_care',        'help.category.blocked.wound',      5, true, 'mobile_care',   false, false, 902),
    ('lifting_transfer',  'help.category.blocked.lifting',    5, true, 'mobile_care',   false, false, 903),
    ('overnight_care',    'help.category.blocked.overnight',  5, true, 'care_agency',   false, false, 904),
    ('paid_domestic',     'help.category.blocked.paid',       5, true, 'employment',    false, false, 905)
ON CONFLICT (code) DO NOTHING;

CREATE TABLE referral_directory (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    referral_group  text NOT NULL,
    region_code     text NOT NULL,          -- postal code prefix or Bezirk code
    name            text NOT NULL,
    phone           text,
    website         text,
    address         text,
    note_key        text,
    is_active       boolean NOT NULL DEFAULT true
);

CREATE INDEX ix_referral_lookup
    ON referral_directory (referral_group, region_code) WHERE is_active;

COMMIT;

-- =============================================================================
-- Verification queries — run these after seeding a test dataset
-- =============================================================================

-- 1. Tenant isolation: must return 0 rows for any org other than the one asked for
-- SELECT DISTINCT organization_id FROM activities WHERE organization_id <> :org;

-- 2. Funder leak check: the funder views must expose no identifier columns.
--    Expect ZERO rows.
-- SELECT table_name, column_name
--   FROM information_schema.columns
--  WHERE table_name LIKE 'v_funder_%'
--    AND (column_name LIKE '%user_id%' OR column_name LIKE '%name%'
--         OR column_name LIKE '%email%' OR column_name LIKE '%phone%'
--         OR column_name LIKE '%address%' OR column_name LIKE '%note%');

-- 3. Hours reconciliation: the view total must equal a hand-computed control set.
-- SELECT organization_id, occurred_month, SUM(hours)
--   FROM v_volunteer_hours GROUP BY 1,2 ORDER BY 1,2;

-- 4. BR-TRANSPORT-04 must be unbypassable. Expect an error.
-- INSERT INTO activities (..., transport_mode, insurance_context, status)
-- VALUES (..., 'volunteer_private_vehicle', 'unknown', 'confirmed');
