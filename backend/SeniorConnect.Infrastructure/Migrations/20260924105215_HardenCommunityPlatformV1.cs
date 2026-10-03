using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenCommunityPlatformV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPendingReview",
                schema: "public",
                table: "CommunityEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "public",
                table: "CommunityEvents",
                type: "xmin",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // Rows written before these unique indexes existed may contain the very
            // duplicates the indexes forbid (P5-02 double membership, P5-05 double RSVP).
            // Keep exactly one row per key, preferring the one that must not be lost:
            // memberships — a ban (6) first, then the owner row, then Active/Muted (1/5);
            // registrations — a non-cancelled (Cancelled = 3) RSVP, newest first;
            // reports — the first report.
            migrationBuilder.Sql("""
                DELETE FROM "public"."GroupMemberships" t
                USING (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "GroupId", "UserId"
                        ORDER BY ("Status" = 6) DESC, ("Role" = 0) DESC, ("Status" IN (1, 5)) DESC, "UpdatedAtUtc" DESC) AS rn
                    FROM "public"."GroupMemberships") d
                WHERE t."Id" = d."Id" AND d.rn > 1;

                DELETE FROM "public"."EventRegistrations" t
                USING (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "EventId", "UserId"
                        ORDER BY ("Status" = 3) ASC, "UpdatedAtUtc" DESC) AS rn
                    FROM "public"."EventRegistrations") d
                WHERE t."Id" = d."Id" AND d.rn > 1;

                DELETE FROM "public"."CommunityReports" t
                USING (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "ReporterUserId", "TargetType", "TargetId"
                        ORDER BY "CreatedAtUtc" ASC) AS rn
                    FROM "public"."CommunityReports") d
                WHERE t."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_group_memberships_group_user",
                schema: "public",
                table: "GroupMemberships",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_event_registrations_event_user",
                schema: "public",
                table: "EventRegistrations",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_community_reports_reporter_target",
                schema: "public",
                table: "CommunityReports",
                columns: new[] { "ReporterUserId", "TargetType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_group_memberships_group_user",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropIndex(
                name: "ux_event_registrations_event_user",
                schema: "public",
                table: "EventRegistrations");

            migrationBuilder.DropIndex(
                name: "ux_community_reports_reporter_target",
                schema: "public",
                table: "CommunityReports");

            migrationBuilder.DropColumn(
                name: "IsPendingReview",
                schema: "public",
                table: "CommunityEvents");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "public",
                table: "CommunityEvents");
        }
    }
}
