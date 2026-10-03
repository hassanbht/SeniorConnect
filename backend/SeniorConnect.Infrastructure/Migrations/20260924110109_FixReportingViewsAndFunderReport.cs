using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <summary>
    /// P2-13 / P2-17/18 / P2-29 / P2-33/35 reporting-view fixes. The applied
    /// AddReportingViews migration is left untouched; this one replaces its
    /// objects:
    ///
    ///  · v_volunteer_hours — excludes soft-deleted activities; adds
    ///    subject_user_id + category_code so staff reports can count DISTINCT
    ///    people and split hours by category from one source.
    ///  · v_funder_monthly_report — previously only in
    ///    database/migrations/002_phase2_wedge.sql, so the funder endpoints
    ///    failed on an EF-built database. Confirmed, non-deleted activities
    ///    only; a row below a cohort of 10 people OR volunteers (BR-FUNDER-03)
    ///    has every measure NULLed. No identifiers, no free text (BR-FUNDER-02).
    ///  · mv_volunteer_roster — BR-ROSTER-01: completed (Confirmed,
    ///    non-deleted) activities, Active ≤ 60 days, Dormant ≤ 120 days, with
    ///    per-org overrides from organization_policies 'roster.thresholds'
    ///    (BR-ROSTER-02) — the same rule as RosterStatusPolicy in code.
    /// </summary>
    public partial class FixReportingViewsAndFunderReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_funder_monthly_report;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_volunteer_hours;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_volunteer_roster;");

            migrationBuilder.Sql("""
                CREATE VIEW v_volunteer_hours AS
                SELECT a.volunteer_user_id,
                       a.organization_id,
                       a.subject_user_id,
                       c.code                                                     AS category_code,
                       a.occurred_on,
                       date_trunc('month', a.occurred_on)::date                   AS occurred_month,
                       COUNT(*)                                                   AS activity_count,
                       SUM(a.duration_minutes)                                    AS minutes,
                       ROUND(SUM(a.duration_minutes) / 60.0, 2)::double precision AS hours
                FROM activities a
                JOIN activity_categories c ON c.id = a.category_id
                WHERE a.status = 'Confirmed'
                  AND a.is_deleted = false
                GROUP BY a.volunteer_user_id, a.organization_id, a.subject_user_id, c.code, a.occurred_on;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW v_funder_monthly_report AS
                WITH facts AS (
                    SELECT fr.funder_id,
                           a.organization_id,
                           date_trunc('month', a.occurred_on)::date  AS month,
                           c.code                                    AS category_code,
                           COUNT(*)                                  AS activity_count,
                           COUNT(DISTINCT a.volunteer_user_id)       AS distinct_volunteers,
                           COUNT(DISTINCT a.subject_user_id)         AS distinct_people_supported,
                           ROUND(SUM(a.duration_minutes) / 60.0, 1)  AS hours
                    FROM activities a
                    JOIN activity_categories c ON c.id = a.category_id
                    JOIN funding_relationships fr
                           ON fr.organization_id = a.organization_id
                          AND date_trunc('month', a.occurred_on) >= date_trunc('month', fr.valid_from)
                          AND (fr.valid_until IS NULL
                               OR date_trunc('month', a.occurred_on) <= date_trunc('month', fr.valid_until))
                    WHERE a.status = 'Confirmed'
                      AND a.is_deleted = false
                    GROUP BY fr.funder_id, a.organization_id, date_trunc('month', a.occurred_on), c.code
                )
                SELECT funder_id,
                       organization_id,
                       month,
                       category_code,
                       CASE WHEN suppressed THEN NULL ELSE activity_count::int END            AS activity_count,
                       CASE WHEN suppressed THEN NULL ELSE distinct_volunteers::int END       AS distinct_volunteers,
                       CASE WHEN suppressed THEN NULL ELSE distinct_people_supported::int END AS distinct_people_supported,
                       CASE WHEN suppressed THEN NULL ELSE hours::double precision END        AS hours,
                       suppressed                                                             AS is_suppressed
                FROM (
                    SELECT f.*,
                           (f.distinct_people_supported < 10 OR f.distinct_volunteers < 10) AS suppressed
                    FROM facts f
                ) s;
                """);

            migrationBuilder.Sql("""
                COMMENT ON VIEW v_funder_monthly_report IS
                  'BR-FUNDER-03. A NULL metric with is_suppressed = true renders as "<10". Never render 0 for a suppressed cell.';
                """);

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_volunteer_roster AS
                WITH thresholds AS (
                    SELECT om.organization_id,
                           om.user_id,
                           CASE WHEN (p.policy_value ->> 'activeDays') ~ '^[1-9][0-9]{0,4}$' THEN (p.policy_value ->> 'activeDays')::int ELSE 60 END   AS active_days,
                           CASE WHEN (p.policy_value ->> 'dormantDays') ~ '^[1-9][0-9]{0,4}$' THEN (p.policy_value ->> 'dormantDays')::int ELSE 120 END AS dormant_days
                    FROM organization_memberships om
                    LEFT JOIN organization_policies p
                           ON p.organization_id = om.organization_id
                          AND p.policy_key = 'roster.thresholds'
                    WHERE om.role   = 'Volunteer'
                      AND om.status = 'Active'
                )
                SELECT t.organization_id,
                       t.user_id,
                       MAX(a.occurred_on) AS last_activity_on,
                       COUNT(a.id) FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365) AS activities_12m,
                       COALESCE(SUM(a.duration_minutes)
                                FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365), 0) AS minutes_12m,
                       CASE
                         WHEN MAX(a.occurred_on) IS NULL THEN 'NeverActivated'
                         WHEN CURRENT_DATE - MAX(a.occurred_on) <= MAX(t.active_days) THEN 'Active'
                         WHEN CURRENT_DATE - MAX(a.occurred_on) <= MAX(t.dormant_days) THEN 'Dormant'
                         ELSE 'Inactive'
                       END AS roster_status
                FROM thresholds t
                LEFT JOIN activities a
                       ON a.volunteer_user_id = t.user_id
                      AND a.organization_id   = t.organization_id
                      AND a.status            = 'Confirmed'
                      AND a.is_deleted        = false
                GROUP BY t.organization_id, t.user_id;
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_mv_roster ON mv_volunteer_roster (organization_id, user_id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_mv_roster_status ON mv_volunteer_roster (organization_id, roster_status);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_funder_monthly_report;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_volunteer_hours;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_volunteer_roster;");

            // Restore the AddReportingViews definitions.
            migrationBuilder.Sql("""
                CREATE VIEW v_volunteer_hours AS
                SELECT a.volunteer_user_id,
                       a.organization_id,
                       a.occurred_on,
                       date_trunc('month', a.occurred_on)::date AS occurred_month,
                       COUNT(*)                                  AS activity_count,
                       SUM(a.duration_minutes)                   AS minutes,
                       ROUND(SUM(a.duration_minutes) / 60.0, 2)  AS hours
                FROM activities a
                WHERE a.status = 'Confirmed'
                GROUP BY a.volunteer_user_id, a.organization_id, a.occurred_on;
                """);

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_volunteer_roster AS
                SELECT om.organization_id,
                       om.user_id,
                       MAX(a.occurred_on) AS last_activity_on,
                       COUNT(a.id) FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365) AS activities_12m,
                       COALESCE(SUM(a.duration_minutes)
                                FILTER (WHERE a.occurred_on >= CURRENT_DATE - 365), 0) AS minutes_12m,
                       CASE
                         WHEN MAX(a.occurred_on) IS NULL THEN 'NeverActivated'
                         WHEN MAX(a.occurred_on) >= CURRENT_DATE - INTERVAL '3 months' THEN 'Active'
                         WHEN MAX(a.occurred_on) >= CURRENT_DATE - INTERVAL '6 months' THEN 'Dormant'
                         ELSE 'Inactive'
                       END AS roster_status
                FROM organization_memberships om
                LEFT JOIN activities a
                       ON a.volunteer_user_id = om.user_id
                      AND a.organization_id   = om.organization_id
                      AND a.status            = 'Confirmed'
                WHERE om.role   = 'Volunteer'
                  AND om.status = 'Active'
                GROUP BY om.organization_id, om.user_id;
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_mv_roster ON mv_volunteer_roster (organization_id, user_id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_mv_roster_status ON mv_volunteer_roster (organization_id, roster_status);");
        }
    }
}
