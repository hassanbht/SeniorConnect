using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedPolicyImplementationS2S4B1B2Adr026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConvertedAtUtc",
                schema: "public",
                table: "CommunityReports",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedByOfficerUserId",
                schema: "public",
                table: "CommunityReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SafeguardingCaseId",
                schema: "public",
                table: "CommunityReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_tasks",
                schema: "safeguarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "visit_address_grants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    help_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    volunteer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_by_role = table.Column<string>(type: "text", nullable: false),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_address_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operational_tasks_open_per_subject",
                schema: "safeguarding",
                table: "operational_tasks",
                columns: new[] { "organization_id", "subject_user_id", "completed_at_utc", "cancelled_at_utc" },
                filter: "completed_at_utc IS NULL AND cancelled_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_visit_address_grants_request",
                schema: "public",
                table: "visit_address_grants",
                column: "help_request_id");

            migrationBuilder.CreateIndex(
                name: "ux_visit_address_grants_live",
                schema: "public",
                table: "visit_address_grants",
                columns: new[] { "help_request_id", "volunteer_user_id", "revoked_at_utc" },
                unique: true,
                filter: "revoked_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_tasks",
                schema: "safeguarding");

            migrationBuilder.DropTable(
                name: "visit_address_grants",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "ConvertedAtUtc",
                schema: "public",
                table: "CommunityReports");

            migrationBuilder.DropColumn(
                name: "ConvertedByOfficerUserId",
                schema: "public",
                table: "CommunityReports");

            migrationBuilder.DropColumn(
                name: "SafeguardingCaseId",
                schema: "public",
                table: "CommunityReports");
        }
    }
}
