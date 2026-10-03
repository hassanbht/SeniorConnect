-- =============================================================================
-- Mitanand — Scope Generalization migration (ADR-018)
--
-- From "app for seniors" to a general mutual-aid platform: seniors, migrants
-- and newcomers, families, volunteers, mentors and community groups.
--
-- This is a RENAME and an ADDITION, not a redesign. Depends on 001 and 002.
-- Run this BEFORE building the Phase 3 mobile screens (P3-21..P3-24) — see
-- PHASE-AUDIT-2026-08.md. Building those screens against the old naming and
-- category set first would mean redoing them.
--
-- Verified against PostgreSQL 16.15, applied on top of 001 + 002.
-- =============================================================================

BEGIN;

-- =============================================================================
-- 1. Rename senior_profiles -> support_profiles (ADR-018 §3)
--
-- Same columns, broadened meaning. volunteer_profiles is untouched —
-- "volunteer" was never age-specific.
-- =============================================================================

ALTER TABLE senior_profiles RENAME TO support_profiles;

-- Constraint and index names carry the old table name by convention.
-- Renaming them keeps \d output and future migrations honest.
ALTER TABLE support_profiles
    RENAME CONSTRAINT ck_senior_contact_method TO ck_support_contact_method;
ALTER TABLE support_profiles
    RENAME CONSTRAINT ck_senior_vulnerability_reason TO ck_support_vulnerability_reason;
ALTER TABLE support_profiles
    RENAME CONSTRAINT ck_senior_geo_pair TO ck_support_geo_pair;

ALTER INDEX ix_senior_profiles_geo RENAME TO ix_support_profiles_geo;
ALTER INDEX ix_senior_profiles_postal RENAME TO ix_support_profiles_postal;

COMMENT ON TABLE support_profiles IS
    'Anyone receiving support: seniors, newcomers, isolated parents, people '
    'with disabilities, or any other circumstance. Not exclusive to seniors. '
    'See ADR-018.';

COMMENT ON COLUMN support_profiles.mobility_note IS
    'FUNCTIONAL need only, in the user''s own words (e.g. "third floor, no '
    'lift"). Never a diagnosis, never a legal/immigration status. BR-GDPR-02.';

-- =============================================================================
-- 2. Forbidden fields — this migration adds none of them, on purpose.
--
-- Documented here so the intent is visible at the point where someone might
-- otherwise add one. ADR-018 §5 / BR-GDPR-07. The actual enforcement is the
-- NoSensitiveMigrationDataTests architecture test, not a database constraint
-- (a CHECK constraint cannot inspect column existence).
--
-- NEVER ADD, to support_profiles or anywhere else:
--   residency_status · asylum_status · visa_type · citizenship ·
--   ethnicity · religion · immigration_case_number ·
--   country_of_origin AS A MANDATORY FIELD
--
-- "Native language" is fine and already modeled via languages/user_languages
-- — it exists to serve the person (matching), not to classify them.
-- =============================================================================

-- =============================================================================
-- 3. New activity categories (ADR-018 §4) — all low safety level, all slot
--    into the existing category/safety/matching machinery unchanged.
-- =============================================================================

INSERT INTO activity_categories
    (code, name_key, default_safety_level,
     typically_involves_transport, typically_involves_money, sort_order)
VALUES
    ('language_practice',    'category.language_practice',    1, false, false, 45),
    ('newcomer_orientation', 'category.newcomer_orientation', 2, false, false, 46),
    ('mentoring',            'category.mentoring',            2, false, false, 47)
ON CONFLICT (code) DO NOTHING;

-- =============================================================================
-- 4. New interests (ADR-018 §4)
-- =============================================================================

INSERT INTO interests (code, name_key) VALUES
    ('language_exchange',  'interest.language_exchange'),
    ('local_orientation',  'interest.local_orientation'),
    ('job_search_support', 'interest.job_search_support')
ON CONFLICT (code) DO NOTHING;

-- =============================================================================
-- 5. Verify nothing else references the old table name
-- =============================================================================

DO $$
DECLARE
    leftover_count integer;
BEGIN
    SELECT COUNT(*) INTO leftover_count
    FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name = 'senior_profiles';

    IF leftover_count > 0 THEN
        RAISE EXCEPTION 'senior_profiles still exists — rename did not complete';
    END IF;
END $$;

COMMIT;
