-- =============================================================================
-- SeniorConnect — Phase 1 schema (Identity, Profiles, Audit)
-- PostgreSQL 16
--
-- This is the reference schema. In practice you generate it with EF Core
-- migrations — use this file to review what the migration SHOULD produce.
--
-- Conventions:
--   uuid v7 primary keys · snake_case · timestamptz named *_at_utc
--   enums stored as text + CHECK constraint (Postgres enum types are painful
--   to alter, and this product will alter them)
--   xmin is used as the EF concurrency token (no explicit column needed)
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;

-- uuid v7 helper. Replace with pg_uuidv7 or generate in the application if you
-- prefer; what matters is that IDs are time-ordered for index locality.
CREATE OR REPLACE FUNCTION uuid_generate_v7() RETURNS uuid AS $$
DECLARE
    unix_ts_ms bytea;
    uuid_bytes bytea;
BEGIN
    unix_ts_ms := substring(int8send((extract(epoch FROM clock_timestamp()) * 1000)::bigint) FROM 3);
    uuid_bytes := unix_ts_ms || gen_random_bytes(10);
    uuid_bytes := set_byte(uuid_bytes, 6, (b'0111' || get_byte(uuid_bytes, 6)::bit(4))::bit(8)::int);
    uuid_bytes := set_byte(uuid_bytes, 8, (b'10'   || get_byte(uuid_bytes, 8)::bit(6))::bit(8)::int);
    RETURN encode(uuid_bytes, 'hex')::uuid;
END
$$ LANGUAGE plpgsql VOLATILE;

-- Separate schema for safeguarding, created now so DB grants can be set up
-- from day one even though the tables arrive in Phase 5.
CREATE SCHEMA IF NOT EXISTS safeguarding;

-- =============================================================================
-- users
-- =============================================================================

CREATE TABLE users (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    email                   citext,
    email_verified_at_utc   timestamptz,
    phone                   text,
    phone_verified_at_utc   timestamptz,
    -- NULLABLE. A senior or volunteer account has NO password at all (ADR-016).
    password_hash           text,
    primary_auth_method     text        NOT NULL DEFAULT 'phone_otp',
    display_name            text        NOT NULL,
    date_of_birth           date,
    preferred_locale        text        NOT NULL DEFAULT 'de',
    senior_mode_default     boolean     NOT NULL DEFAULT false,
    status                  text        NOT NULL DEFAULT 'active',
    last_login_at_utc       timestamptz,

    created_at_utc          timestamptz NOT NULL DEFAULT now(),
    created_by              uuid,
    updated_at_utc          timestamptz NOT NULL DEFAULT now(),
    updated_by              uuid,
    is_deleted              boolean     NOT NULL DEFAULT false,

    CONSTRAINT ck_users_status
        CHECK (status IN ('active','suspended','deactivated','deleted')),
    CONSTRAINT ck_users_locale
        CHECK (preferred_locale IN ('de','en','fa')),
    CONSTRAINT ck_users_contact
        CHECK (email IS NOT NULL OR phone IS NOT NULL),

    -- ADR-016 / BR-AUTH-01..03
    CONSTRAINT ck_users_auth_method CHECK (primary_auth_method IN
        ('phone_otp','email_magic_link','password')),

    -- A senior or volunteer account cannot accidentally grow a password field.
    CONSTRAINT ck_users_password_only_for_password_auth CHECK (
        (primary_auth_method = 'password') = (password_hash IS NOT NULL)
    ),

    -- Phone-OTP accounts must actually have a phone number.
    CONSTRAINT ck_users_phone_auth_needs_phone CHECK (
        primary_auth_method <> 'phone_otp' OR phone IS NOT NULL
    )
);

CREATE UNIQUE INDEX ux_users_email ON users (email) WHERE email IS NOT NULL AND NOT is_deleted;
CREATE UNIQUE INDEX ux_users_phone ON users (phone) WHERE phone IS NOT NULL AND NOT is_deleted;

-- =============================================================================
-- OTP challenges (ADR-016) — the primary credential for seniors and volunteers
--
-- Nothing here stores a plaintext phone number, email address or code.
-- =============================================================================

CREATE TABLE otp_challenges (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id             uuid REFERENCES users(id) ON DELETE CASCADE,
    channel             text        NOT NULL,
    destination_hash    text        NOT NULL,
    code_hash           text        NOT NULL,
    purpose             text        NOT NULL DEFAULT 'login',
    attempts            smallint    NOT NULL DEFAULT 0,
    max_attempts        smallint    NOT NULL DEFAULT 5,
    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    expires_at_utc      timestamptz NOT NULL DEFAULT now() + interval '5 minutes',
    consumed_at_utc     timestamptz,
    ip_hash             text,

    CONSTRAINT ck_otp_channel CHECK (channel IN ('sms','email')),
    CONSTRAINT ck_otp_purpose CHECK (purpose IN
        ('login','registration','phone_change','email_change','recovery')),
    CONSTRAINT ck_otp_attempts CHECK (attempts <= max_attempts)
);

-- BR-AUTH-07: rate limiting reads this index.
CREATE INDEX ix_otp_active ON otp_challenges (destination_hash, created_at_utc DESC)
    WHERE consumed_at_utc IS NULL;
CREATE INDEX ix_otp_cleanup ON otp_challenges (expires_at_utc);

-- =============================================================================
-- refresh tokens (hashed, revocable per device)
-- =============================================================================

CREATE TABLE refresh_tokens (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      text        NOT NULL,
    device_label    text,
    is_personal_device boolean  NOT NULL DEFAULT true,
    issued_at_utc   timestamptz NOT NULL DEFAULT now(),
    -- 90 days on a personal device (ADR-016 / BR-AUTH-04)
    expires_at_utc  timestamptz NOT NULL DEFAULT now() + interval '90 days',
    revoked_at_utc  timestamptz,
    replaced_by_id  uuid REFERENCES refresh_tokens(id)
);

CREATE INDEX ix_refresh_tokens_user   ON refresh_tokens (user_id) WHERE revoked_at_utc IS NULL;
CREATE UNIQUE INDEX ux_refresh_tokens_hash ON refresh_tokens (token_hash);

-- =============================================================================
-- capabilities  (see docs/architecture/authorization.md)
-- Derived capabilities are recomputed; granted ones are inserted by staff.
-- =============================================================================

CREATE TABLE user_capabilities (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id             uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    capability          text        NOT NULL,
    source              text        NOT NULL DEFAULT 'derived',
    granted_by_user_id  uuid REFERENCES users(id),
    granted_by_org_id   uuid,           -- FK added in Phase 6
    reason              text,
    granted_at_utc      timestamptz NOT NULL DEFAULT now(),
    expires_at_utc      timestamptz,

    CONSTRAINT ck_user_capabilities_source CHECK (source IN ('derived','granted')),
    CONSTRAINT ck_user_capabilities_reason
        CHECK (source = 'derived' OR reason IS NOT NULL)
);

CREATE UNIQUE INDEX ux_user_capabilities
    ON user_capabilities (user_id, capability, COALESCE(granted_by_org_id, '00000000-0000-0000-0000-000000000000'::uuid));
CREATE INDEX ix_user_capabilities_expiring
    ON user_capabilities (expires_at_utc) WHERE expires_at_utc IS NOT NULL;

-- =============================================================================
-- verifications  (outcomes ONLY — never the document.  BR-TRUST-05)
-- =============================================================================

CREATE TABLE verifications (
    id                          uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id                     uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type                        text        NOT NULL,
    status                      text        NOT NULL DEFAULT 'not_started',
    provider                    text        NOT NULL DEFAULT 'manual',
    verified_by_user_id         uuid REFERENCES users(id),
    verified_by_organization_id uuid,       -- FK added in Phase 6
    verified_at_utc             timestamptz,
    valid_until_utc             timestamptz,
    external_reference          text,       -- the provider's id, NOT the document
    rejection_reason            text,

    created_at_utc              timestamptz NOT NULL DEFAULT now(),
    created_by                  uuid,
    updated_at_utc              timestamptz NOT NULL DEFAULT now(),
    updated_by                  uuid,

    CONSTRAINT ck_verifications_type CHECK (type IN
        ('email','phone','identity','address','organization','training','background_check')),
    CONSTRAINT ck_verifications_status CHECK (status IN
        ('not_started','pending','submitted','verified','rejected','expired')),
    CONSTRAINT ck_verifications_provider CHECK (provider IN
        ('manual','organization','id_austria','kyc')),
    CONSTRAINT ck_verifications_verified_needs_date
        CHECK (status <> 'verified' OR verified_at_utc IS NOT NULL)
);

CREATE INDEX ix_verifications_user_type ON verifications (user_id, type, status);
CREATE INDEX ix_verifications_expiring  ON verifications (valid_until_utc)
    WHERE status = 'verified' AND valid_until_utc IS NOT NULL;

-- Snapshot of every computed trust level, so a past decision can be explained.
CREATE TABLE trust_level_snapshots (
    id              uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    level           smallint    NOT NULL,
    reason          jsonb       NOT NULL,
    computed_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_trust_level_range CHECK (level BETWEEN 0 AND 5)
);

CREATE INDEX ix_trust_snapshots_user ON trust_level_snapshots (user_id, computed_at_utc DESC);

-- =============================================================================
-- Profiles
-- =============================================================================

CREATE TABLE senior_profiles (
    id                          uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id                     uuid        NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    address_line                text,
    postal_code                 text,
    city                        text,
    country                     text        NOT NULL DEFAULT 'AT',
    latitude                    double precision,
    longitude                   double precision,

    -- FUNCTIONAL need only, never a diagnosis.  BR-GDPR-02
    mobility_note               text,
    living_situation            text,
    preferred_contact_method    text        NOT NULL DEFAULT 'app',

    -- Set only by authorised staff. Drives Safety Level 5.
    vulnerability_flag          boolean     NOT NULL DEFAULT false,
    vulnerability_set_by_user_id uuid REFERENCES users(id),
    vulnerability_reason        text,

    created_at_utc              timestamptz NOT NULL DEFAULT now(),
    created_by                  uuid,
    updated_at_utc              timestamptz NOT NULL DEFAULT now(),
    updated_by                  uuid,

    CONSTRAINT ck_senior_contact_method
        CHECK (preferred_contact_method IN ('app','phone','sms','family')),
    CONSTRAINT ck_senior_vulnerability_reason
        CHECK (NOT vulnerability_flag OR vulnerability_reason IS NOT NULL),
    CONSTRAINT ck_senior_geo_pair
        CHECK ((latitude IS NULL) = (longitude IS NULL))
);

CREATE INDEX ix_senior_profiles_geo ON senior_profiles (latitude, longitude)
    WHERE latitude IS NOT NULL;
CREATE INDEX ix_senior_profiles_postal ON senior_profiles (postal_code);

CREATE TABLE volunteer_profiles (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id                 uuid        NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    bio                     text,
    postal_code             text,
    latitude                double precision,
    longitude               double precision,
    max_distance_km         integer     NOT NULL DEFAULT 10,
    max_activities_per_week smallint    NOT NULL DEFAULT 3,
    has_car                 boolean     NOT NULL DEFAULT false,
    is_accepting_requests   boolean     NOT NULL DEFAULT true,

    -- Computed from behaviour, never user-editable. Phase 3 populates it.
    reliability_score       numeric(4,3),
    active_since_utc        timestamptz,

    created_at_utc          timestamptz NOT NULL DEFAULT now(),
    created_by              uuid,
    updated_at_utc          timestamptz NOT NULL DEFAULT now(),
    updated_by              uuid,

    CONSTRAINT ck_volunteer_distance CHECK (max_distance_km BETWEEN 1 AND 100),
    CONSTRAINT ck_volunteer_weekly   CHECK (max_activities_per_week BETWEEN 1 AND 40),
    CONSTRAINT ck_volunteer_reliability
        CHECK (reliability_score IS NULL OR reliability_score BETWEEN 0 AND 1),
    CONSTRAINT ck_volunteer_geo_pair
        CHECK ((latitude IS NULL) = (longitude IS NULL))
);

CREATE INDEX ix_volunteer_profiles_geo ON volunteer_profiles (latitude, longitude)
    WHERE latitude IS NOT NULL AND is_accepting_requests;

-- =============================================================================
-- Reference data
-- =============================================================================

CREATE TABLE interests (
    id        uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    code      text NOT NULL UNIQUE,
    name_key  text NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE user_interests (
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    interest_id uuid NOT NULL REFERENCES interests(id),
    PRIMARY KEY (user_id, interest_id)
);

CREATE TABLE languages (
    id       uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    iso_code text NOT NULL UNIQUE,
    name_key text NOT NULL
);

CREATE TABLE user_languages (
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    language_id uuid NOT NULL REFERENCES languages(id),
    proficiency text NOT NULL DEFAULT 'b1',
    PRIMARY KEY (user_id, language_id),
    CONSTRAINT ck_user_languages_proficiency
        CHECK (proficiency IN ('a1','a2','b1','b2','c1','c2','native'))
);

CREATE TABLE skills (
    id                   uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    code                 text NOT NULL UNIQUE,
    name_key             text NOT NULL,
    requires_verification boolean NOT NULL DEFAULT false,
    is_active            boolean NOT NULL DEFAULT true
);

CREATE TABLE volunteer_skills (
    volunteer_profile_id uuid NOT NULL REFERENCES volunteer_profiles(id) ON DELETE CASCADE,
    skill_id             uuid NOT NULL REFERENCES skills(id),
    verified_at_utc      timestamptz,
    verified_by_org_id   uuid,
    PRIMARY KEY (volunteer_profile_id, skill_id)
);

CREATE TABLE availability_slots (
    id           uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id      uuid     NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    day_of_week  smallint NOT NULL,
    start_time   time     NOT NULL,
    end_time     time     NOT NULL,
    valid_from   date,
    valid_until  date,

    CONSTRAINT ck_availability_dow   CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_availability_order CHECK (start_time < end_time)
);

CREATE INDEX ix_availability_user ON availability_slots (user_id, day_of_week);

-- =============================================================================
-- Audit — APPEND ONLY.  BR-AUDIT-03
-- =============================================================================

CREATE TABLE audit_entries (
    id                      uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    actor_user_id           uuid REFERENCES users(id),
    actor_organization_id   uuid,
    action                  text        NOT NULL,
    subject_type            text        NOT NULL,
    subject_id              uuid,
    reason                  text,
    metadata                jsonb,
    correlation_id          text,
    ip_hash                 text,       -- salted hash, never a raw IP
    at_utc                  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_audit_subject ON audit_entries (subject_type, subject_id, at_utc DESC);
CREATE INDEX ix_audit_actor   ON audit_entries (actor_user_id, at_utc DESC);
CREATE INDEX ix_audit_action  ON audit_entries (action, at_utc DESC);

-- Enforce append-only at the database level, not by developer discipline.
REVOKE UPDATE, DELETE ON audit_entries FROM PUBLIC;
-- Then, for your application role:
--   GRANT SELECT, INSERT ON audit_entries TO SeniorConnect_app;

-- =============================================================================
-- Consents (needed from Phase 1 so terms acceptance is recorded at registration)
-- =============================================================================

CREATE TABLE consents (
    id               uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id          uuid        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    consent_type     text        NOT NULL,
    document_version text        NOT NULL,
    granted          boolean     NOT NULL,
    granted_at_utc   timestamptz NOT NULL DEFAULT now(),
    withdrawn_at_utc timestamptz,
    ip_hash          text,

    CONSTRAINT ck_consents_type CHECK (consent_type IN
        ('terms','privacy','notifications_push','notifications_sms',
         'notifications_email','research_statistics'))
);

CREATE INDEX ix_consents_user ON consents (user_id, consent_type, granted_at_utc DESC);

-- =============================================================================
-- updated_at_utc maintenance
-- One trigger, one purpose, no business logic. This is the only trigger the
-- architecture permits without an ADR.
-- =============================================================================

CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at_utc := now();
    RETURN NEW;
END
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_users_updated              BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER tr_senior_profiles_updated    BEFORE UPDATE ON senior_profiles
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER tr_volunteer_profiles_updated BEFORE UPDATE ON volunteer_profiles
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER tr_verifications_updated      BEFORE UPDATE ON verifications
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- =============================================================================
-- Seed: interests and languages (extend during Phase 0 with real Gemeinde data)
-- =============================================================================

INSERT INTO interests (code, name_key) VALUES
    ('walking',      'interest.walking'),
    ('gardening',    'interest.gardening'),
    ('cooking',      'interest.cooking'),
    ('cards_games',  'interest.cards_games'),
    ('chess',        'interest.chess'),
    ('reading',      'interest.reading'),
    ('music',        'interest.music'),
    ('handicraft',   'interest.handicraft'),
    ('coffee_talk',  'interest.coffee_talk'),
    ('language_exchange', 'interest.language_exchange'),
    ('photography',  'interest.photography'),
    ('church',       'interest.church'),
    ('exercise',     'interest.exercise'),
    ('animals',      'interest.animals')
ON CONFLICT (code) DO NOTHING;

INSERT INTO languages (iso_code, name_key) VALUES
    ('de','language.de'), ('en','language.en'), ('fa','language.fa'),
    ('tr','language.tr'), ('bs','language.bs'), ('hr','language.hr'),
    ('sr','language.sr'), ('ar','language.ar'), ('uk','language.uk'),
    ('ro','language.ro'), ('hu','language.hu'), ('it','language.it')
ON CONFLICT (iso_code) DO NOTHING;

INSERT INTO skills (code, name_key, requires_verification) VALUES
    ('first_aid',        'skill.first_aid',        true),
    ('driving',          'skill.driving',          true),
    ('elderly_support',  'skill.elderly_support',  false),
    ('shopping_help',    'skill.shopping_help',    false),
    ('tech_help',        'skill.tech_help',        false),
    ('paperwork_help',   'skill.paperwork_help',   false),
    ('conversation',     'skill.conversation',     false),
    ('gardening_help',   'skill.gardening_help',   false)
ON CONFLICT (code) DO NOTHING;

-- =============================================================================
-- NOT in Phase 1 — deliberately absent, added in later migrations:
--   organizations, organization_branches, organization_memberships   (Phase 6)
--   community_groups, group_members, events, event_registrations     (Phase 2)
--   help_request_categories, help_requests, help_offers,
--   activity_checkins, volunteer_hours, activity_feedback            (Phase 3)
--   family_relationships, family_permissions, trusted_contacts       (Phase 4)
--   trainings, user_trainings, user_blocks                           (Phase 5)
--   safeguarding.cases, case_notes, case_actions, case_access_log    (Phase 5)
--   notifications, notification_preferences, devices                 (Phase 7)
-- =============================================================================
