using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P2-13: a VIEW over confirmed activities, never a second table.
            // Column set matches VolunteerHoursViewConfiguration exactly.
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

            // P2-18: roster status derived from behaviour, materialised for
            // dashboard speed. Thresholds (3 / 6 months) match the live
            // per-request computation in CoordinatorEndpoints.cs (P2-17) so
            // the two never disagree.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_volunteer_roster;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_volunteer_hours;");
        }
    }
}
