using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4VerificationDiscoverySafeguarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_verifications_expiring",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_provider",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications");

            migrationBuilder.AddColumn<string>(
                name: "method",
                schema: "public",
                table: "verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revocation_reason",
                schema: "public",
                table: "verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revoked_at_utc",
                schema: "public",
                table: "verifications",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revoked_by_user_id",
                schema: "public",
                table: "verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VenueType",
                schema: "public",
                table: "CommunityEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "discovery_preferences",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_discoverable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discovery_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "restrictions",
                schema: "safeguarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counterpart_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    blocks_discovery = table.Column<bool>(type: "boolean", nullable: false),
                    blocks_location = table.Column<bool>(type: "boolean", nullable: false),
                    blocks_contact = table.Column<bool>(type: "boolean", nullable: false),
                    blocks_assignment = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_officer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    lifted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lifted_by_officer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lift_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restrictions", x => x.id);
                    table.CheckConstraint("ck_restrictions_any_kind", "blocks_discovery OR blocks_location OR blocks_contact OR blocks_assignment");
                    table.CheckConstraint("ck_restrictions_lifted_needs_reason", "lifted_at_utc IS NULL OR (lifted_by_officer_user_id IS NOT NULL AND lift_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_restrictions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "safeguarding",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_verifications_expiring",
                schema: "public",
                table: "verifications",
                column: "valid_until_utc",
                filter: "status = 'Verified' AND valid_until_utc IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_method",
                schema: "public",
                table: "verifications",
                sql: "method IS NULL OR method IN ('DocumentSighted','OrganizationRecord','OrganizationMembership')");

            // The old constraint was lowercase while EF writes enum names;
            // normalise any row written by hand before the synced constraint.
            migrationBuilder.Sql(@"
UPDATE public.verifications SET provider = 'Manual' WHERE provider = 'manual';
UPDATE public.verifications SET provider = 'Organization' WHERE provider = 'organization';
UPDATE public.verifications SET provider = 'IdAustria' WHERE provider = 'id_austria';
UPDATE public.verifications SET provider = 'Kyc' WHERE provider = 'kyc';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_provider",
                schema: "public",
                table: "verifications",
                sql: "provider IN ('Manual','Organization','IdAustria','Kyc')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_revoked_needs_date",
                schema: "public",
                table: "verifications",
                sql: "status <> 'Revoked' OR revoked_at_utc IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications",
                sql: "status IN ('NotStarted','Pending','Submitted','Verified','Rejected','Expired','Revoked')");

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocked",
                schema: "public",
                table: "UserBlocks",
                column: "BlockedUserId");

            // Blocking twice used to insert a second row; keep the earliest.
            migrationBuilder.Sql(@"
DELETE FROM public.""UserBlocks"" b
 USING public.""UserBlocks"" o
 WHERE b.""BlockingUserId"" = o.""BlockingUserId""
   AND b.""BlockedUserId"" = o.""BlockedUserId""
   AND (b.""CreatedAtUtc"", b.""Id"") > (o.""CreatedAtUtc"", o.""Id"");");

            migrationBuilder.CreateIndex(
                name: "ux_user_blocks_pair",
                schema: "public",
                table: "UserBlocks",
                columns: new[] { "BlockingUserId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_discovery_preferences_user",
                schema: "public",
                table: "discovery_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restrictions_active_counterpart",
                schema: "safeguarding",
                table: "restrictions",
                column: "counterpart_user_id",
                filter: "lifted_at_utc IS NULL AND counterpart_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_restrictions_active_subject",
                schema: "safeguarding",
                table: "restrictions",
                column: "subject_user_id",
                filter: "lifted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_restrictions_case_id",
                schema: "safeguarding",
                table: "restrictions",
                column: "case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discovery_preferences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "restrictions",
                schema: "safeguarding");

            migrationBuilder.DropIndex(
                name: "ix_verifications_expiring",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_method",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_provider",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_revoked_needs_date",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropIndex(
                name: "ix_user_blocks_blocked",
                schema: "public",
                table: "UserBlocks");

            migrationBuilder.DropIndex(
                name: "ux_user_blocks_pair",
                schema: "public",
                table: "UserBlocks");

            migrationBuilder.DropColumn(
                name: "method",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropColumn(
                name: "revocation_reason",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropColumn(
                name: "revoked_at_utc",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropColumn(
                name: "revoked_by_user_id",
                schema: "public",
                table: "verifications");

            migrationBuilder.DropColumn(
                name: "VenueType",
                schema: "public",
                table: "CommunityEvents");

            migrationBuilder.CreateIndex(
                name: "ix_verifications_expiring",
                schema: "public",
                table: "verifications",
                column: "valid_until_utc",
                filter: "status = 'verified' AND valid_until_utc IS NOT NULL");

            // Down is lossy on purpose: revoked facts become expired (they
            // still do not count), and provider values return to the old
            // lowercase constraint.
            migrationBuilder.Sql(@"
UPDATE public.verifications SET status = 'Expired' WHERE status = 'Revoked';
UPDATE public.verifications SET provider = 'manual' WHERE provider = 'Manual';
UPDATE public.verifications SET provider = 'organization' WHERE provider = 'Organization';
UPDATE public.verifications SET provider = 'id_austria' WHERE provider = 'IdAustria';
UPDATE public.verifications SET provider = 'kyc' WHERE provider = 'Kyc';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_provider",
                schema: "public",
                table: "verifications",
                sql: "provider IN ('manual','organization','id_austria','kyc')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_verifications_status",
                schema: "public",
                table: "verifications",
                sql: "status IN ('NotStarted','Pending','Submitted','Verified','Rejected','Expired')");
        }
    }
}
