using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceLegalGateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedByUserId",
                schema: "public",
                table: "Zugangskarten",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FailedAttempts",
                schema: "public",
                table: "Zugangskarten",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "MaxAttempts",
                schema: "public",
                table: "Zugangskarten",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "QrToken",
                schema: "public",
                table: "Zugangskarten",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "FailedAttempts",
                schema: "public",
                table: "FamilyRelationships",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "MaxAttempts",
                schema: "public",
                table: "FamilyRelationships",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "data_processing_agreements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    controller_role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    counterparty_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    counterparty_contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    document_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_processing_agreements", x => x.id);
                    table.CheckConstraint("ck_dpa_controller_role", "controller_role IN ('PlatformIsController','PlatformIsProcessor','JointController')");
                    table.CheckConstraint("ck_dpa_status", "status IN ('Draft','Sent','Executed','Expired')");
                });

            migrationBuilder.CreateTable(
                name: "dpia_records",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    risk_description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    affected_vulnerable_groups = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    mitigation_measures = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    approval_status = table.Column<string>(type: "text", nullable: false),
                    conducted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    conducted_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    review_due_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dpia_records", x => x.id);
                    table.CheckConstraint("ck_dpia_approval_status", "approval_status IN ('NotStarted','Draft','UnderReview','Approved')");
                });

            migrationBuilder.CreateTable(
                name: "hosting_attestations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hosting_provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data_center_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dpa_with_provider_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contract_signed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hosting_attestations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "insurance_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_type = table.Column<string>(type: "text", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    policy_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    coverage_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    coverage_start_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    coverage_end_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    written_confirmation_received_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurance_policies", x => x.id);
                    table.CheckConstraint("ck_insurance_policy_type", "policy_type IN ('VolunteerAccident','TransportLiability','GeneralLiability')");
                });

            migrationBuilder.CreateTable(
                name: "legal_entity_profiles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    legal_form = table.Column<string>(type: "text", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registered_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    vat_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    dpo_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dpo_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    established_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    privacy_policy_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    privacy_policy_lawyer_reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    terms_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    terms_lawyer_reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entity_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processing_activity_records",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    purpose_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    data_categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    data_subject_categories = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    legal_basis = table.Column<string>(type: "text", nullable: false),
                    retention_period_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recipient_categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    involves_third_country_transfer = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_activity_records", x => x.id);
                    table.CheckConstraint("ck_par_legal_basis", "legal_basis IN ('Consent','Contract','LegitimateInterest','LegalObligation','VitalInterest')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_dpa_organization_id",
                schema: "public",
                table: "data_processing_agreements",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_policy_org_type",
                schema: "public",
                table: "insurance_policies",
                columns: new[] { "organization_id", "policy_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_processing_agreements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "dpia_records",
                schema: "public");

            migrationBuilder.DropTable(
                name: "hosting_attestations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "insurance_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "legal_entity_profiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "processing_activity_records",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                schema: "public",
                table: "Zugangskarten");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                schema: "public",
                table: "Zugangskarten");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                schema: "public",
                table: "Zugangskarten");

            migrationBuilder.DropColumn(
                name: "QrToken",
                schema: "public",
                table: "Zugangskarten");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                schema: "public",
                table: "FamilyRelationships");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                schema: "public",
                table: "FamilyRelationships");
        }
    }
}
