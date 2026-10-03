-- =============================================================================
-- Schema constraint tests
--
-- These verify that the safety-critical rules are enforced by the DATABASE,
-- not only by the application. Run against a scratch database after applying
-- 001 and 002. Every "MUST FAIL" block is expected to raise an error.
--
--   createdb SeniorConnect_test
--   psql -d SeniorConnect_test -f 001_phase1_init.sql
--   psql -d SeniorConnect_test -f 002_phase2_wedge.sql
--   psql -d SeniorConnect_test -f 900_schema_tests.sql
--
-- Verified against PostgreSQL 16.15 — all nine pass.
-- =============================================================================

\set ON_ERROR_STOP off

INSERT INTO organizations (id, name, type) VALUES
 ('00000000-0000-0000-0000-0000000000a1','Sozialverein Tirol','ngo'),
 ('00000000-0000-0000-0000-0000000000a2','Nachbarschaft Hall','association');

INSERT INTO users (id, phone, display_name, primary_auth_method) VALUES
 ('00000000-0000-0000-0000-0000000000b1','+4311','Anna V','phone_otp'),
 ('00000000-0000-0000-0000-0000000000b2','+4312','Maria S','phone_otp');

\echo ''
\echo '=== TEST 1  BR-TRANSPORT-04: confirm private-vehicle activity, insurance unknown  [MUST FAIL]'
INSERT INTO activities (organization_id,volunteer_user_id,subject_user_id,category_id,
  occurred_on,duration_minutes,source,logged_by_user_id,involves_transport,transport_mode,
  insurance_context,status,confirmed_by_user_id,confirmed_at_utc)
SELECT '00000000-0000-0000-0000-0000000000a1','00000000-0000-0000-0000-0000000000b1',
  '00000000-0000-0000-0000-0000000000b2',id,CURRENT_DATE,60,'coordinator_logged',
  '00000000-0000-0000-0000-0000000000b1',true,'volunteer_private_vehicle','unknown',
  'confirmed','00000000-0000-0000-0000-0000000000b1',now()
FROM activity_categories WHERE code='doctor_visit';
-- EXPECT: ck_activities_transport_insurance

\echo ''
\echo '=== TEST 2  same activity, insurance resolved  [MUST SUCCEED]'
INSERT INTO activities (organization_id,volunteer_user_id,subject_user_id,category_id,
  occurred_on,duration_minutes,source,logged_by_user_id,involves_transport,transport_mode,
  insurance_context,status,confirmed_by_user_id,confirmed_at_utc)
SELECT '00000000-0000-0000-0000-0000000000a1','00000000-0000-0000-0000-0000000000b1',
  '00000000-0000-0000-0000-0000000000b2',id,CURRENT_DATE,90,'coordinator_logged',
  '00000000-0000-0000-0000-0000000000b1',true,'volunteer_private_vehicle',
  'organization_covered','confirmed','00000000-0000-0000-0000-0000000000b1',now()
FROM activity_categories WHERE code='doctor_visit';

\echo ''
\echo '=== TEST 3  transport flag inconsistent with transport mode  [MUST FAIL]'
INSERT INTO activities (volunteer_user_id,category_id,occurred_on,duration_minutes,
  source,logged_by_user_id,involves_transport,transport_mode)
SELECT '00000000-0000-0000-0000-0000000000b1',id,CURRENT_DATE,30,'self_logged',
  '00000000-0000-0000-0000-0000000000b1',false,'volunteer_private_vehicle'
FROM activity_categories WHERE code='shopping';
-- EXPECT: ck_activities_transport_consistency

\echo ''
\echo '=== TEST 4  volunteer logged as their own subject  [MUST FAIL]'
INSERT INTO activities (volunteer_user_id,subject_user_id,category_id,occurred_on,
  duration_minutes,source,logged_by_user_id)
SELECT '00000000-0000-0000-0000-0000000000b1','00000000-0000-0000-0000-0000000000b1',
  id,CURRENT_DATE,30,'self_logged','00000000-0000-0000-0000-0000000000b1'
FROM activity_categories WHERE code='shopping';
-- EXPECT: ck_activities_distinct_parties

\echo ''
\echo '=== TEST 5  ADR-007: an activity with NO organization  [MUST SUCCEED]'
INSERT INTO activities (organization_id,volunteer_user_id,subject_user_id,category_id,
  occurred_on,duration_minutes,source,logged_by_user_id,insurance_context,status,
  confirmed_by_user_id,confirmed_at_utc)
SELECT NULL,'00000000-0000-0000-0000-0000000000b1','00000000-0000-0000-0000-0000000000b2',
  id,CURRENT_DATE,45,'self_logged','00000000-0000-0000-0000-0000000000b1',
  'private_neighbourly','confirmed','00000000-0000-0000-0000-0000000000b2',now()
FROM activity_categories WHERE code='shopping';

\echo ''
\echo '=== TEST 6  BR-SCOPE-02: every blocked category has a referral group'
SELECT code, referral_group FROM activity_categories WHERE is_blocked ORDER BY code;
SELECT CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL — blocked category without referral' END
FROM activity_categories WHERE is_blocked AND referral_group IS NULL;

\echo ''
\echo '=== TEST 7  hours view reconciles, including the org-less activity'
SELECT COALESCE(organization_id::text,'(no organization)') AS org,
       SUM(minutes) AS minutes, SUM(hours) AS hours
FROM v_volunteer_hours GROUP BY 1 ORDER BY 1;
-- EXPECT: 90 min for the NGO, 45 min with no organization

\echo ''
\echo '=== TEST 8  ADR-016: a phone-OTP account carrying a password  [MUST FAIL]'
INSERT INTO users (phone,display_name,primary_auth_method,password_hash)
VALUES ('+4399','Hans P','phone_otp','somehash');
-- EXPECT: ck_users_password_only_for_password_auth

\echo ''
\echo '=== TEST 9  BR-FUNDER-02: the funder view must expose no identifying column'
SELECT column_name FROM information_schema.columns
WHERE table_schema='public' AND table_name='v_funder_monthly_report'
ORDER BY ordinal_position;
SELECT CASE WHEN COUNT(*) = 0 THEN 'PASS'
            ELSE 'FAIL — identifying column reachable from the funder view' END
FROM information_schema.columns
WHERE table_schema='public' AND table_name='v_funder_monthly_report'
  AND (column_name ILIKE '%user%' OR column_name ILIKE '%name%'
    OR column_name ILIKE '%phone%' OR column_name ILIKE '%email%'
    OR column_name ILIKE '%address%' OR column_name ILIKE '%note%');
