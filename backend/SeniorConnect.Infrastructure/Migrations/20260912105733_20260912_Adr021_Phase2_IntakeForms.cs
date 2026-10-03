using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260912_Adr021_Phase2_IntakeForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intake_form_fields",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "text", nullable: false),
                    label_key = table.Column<string>(type: "text", nullable: false),
                    field_type = table.Column<string>(type: "text", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_form_fields", x => x.id);
                    table.CheckConstraint("ck_intake_fields_type", "field_type IN ('text','textarea','single_choice','multi_choice','boolean','date','time_slots')");
                });

            migrationBuilder.CreateTable(
                name: "intake_form_sections",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_form_sections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intake_form_submissions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Submitted"),
                    submission_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    criminal_clearance_declared = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criminal_clearance_declared_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    gdpr_consent_accepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    gdpr_consent_accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    event_invitation_opt_in = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_form_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_status", "status IN ('draft','submitted','approved','declined')");
                });

            migrationBuilder.CreateTable(
                name: "organization_intake_forms",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_intake_forms", x => x.id);
                    table.CheckConstraint("ck_intake_forms_type", "form_type IN ('volunteer','help_seeker')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_intake_form_fields_section_order",
                schema: "public",
                table: "intake_form_fields",
                columns: new[] { "section_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_intake_form_sections_form_order",
                schema: "public",
                table: "intake_form_sections",
                columns: new[] { "form_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_form_org_user",
                schema: "public",
                table: "intake_form_submissions",
                columns: new[] { "form_id", "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_org_status",
                schema: "public",
                table: "intake_form_submissions",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_intake_forms_org_type_active",
                schema: "public",
                table: "organization_intake_forms",
                columns: new[] { "organization_id", "form_type", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intake_form_fields",
                schema: "public");

            migrationBuilder.DropTable(
                name: "intake_form_sections",
                schema: "public");

            migrationBuilder.DropTable(
                name: "intake_form_submissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organization_intake_forms",
                schema: "public");
        }
    }
}
