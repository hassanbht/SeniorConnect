using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "activities",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    HelpRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    LocationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InsuranceContext = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InsuranceDisclaimerAcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    InsuranceDisclaimerAcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransportMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LoggedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoggedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xmin", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.CheckConstraint("ck_activities_distinct_parties", "subject_user_id IS NULL OR subject_user_id <> volunteer_user_id");
                    table.CheckConstraint("ck_activities_duration", "duration_minutes BETWEEN 1 AND 1440");
                    table.CheckConstraint("ck_activities_transport_insurance", "transport_mode <> 'volunteer_private_vehicle' OR status <> 'confirmed' OR insurance_context <> 'unknown'");
                });

            migrationBuilder.CreateTable(
                name: "activity_categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    default_safety_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    referral_group = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_categories", x => x.id);
                    table.CheckConstraint("ck_activity_categories_blocked_needs_referral", "NOT is_blocked OR referral_group IS NOT NULL");
                    table.CheckConstraint("ck_activity_categories_safety", "default_safety_level BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    ip_hash = table.Column<string>(type: "text", nullable: true),
                    at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "availability_slots",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_slots", x => x.id);
                    table.CheckConstraint("ck_availability_dow", "day_of_week BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_availability_order", "start_time < end_time");
                });

            migrationBuilder.CreateTable(
                name: "consents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_type = table.Column<string>(type: "text", nullable: false),
                    document_version = table.Column<string>(type: "text", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    withdrawn_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ip_hash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consents", x => x.id);
                    table.CheckConstraint("ck_consents_type", "consent_type IN ('terms','privacy','notifications_push','notifications_sms','notifications_email','research_statistics')");
                });

            migrationBuilder.CreateTable(
                name: "funder_memberships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    funder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false, defaultValue: "Viewer"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Invited"),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funder_memberships", x => x.id);
                    table.CheckConstraint("ck_funder_memberships_role", "role IN ('viewer','admin')");
                    table.CheckConstraint("ck_funder_memberships_status", "status IN ('invited','active','suspended','left')");
                });

            migrationBuilder.CreateTable(
                name: "funders",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funders", x => x.id);
                    table.CheckConstraint("ck_funders_status", "status IN ('active','suspended','ended')");
                    table.CheckConstraint("ck_funders_type", "type IN ('municipality','foundation','public_body','corporate')");
                });

            migrationBuilder.CreateTable(
                name: "funding_relationships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    funder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    reporting_scope = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_relationships", x => x.id);
                    table.CheckConstraint("ck_funding_dates", "valid_until IS NULL OR valid_until > valid_from");
                });

            migrationBuilder.CreateTable(
                name: "interests",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_code = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_branches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_branches", x => x.id);
                    table.CheckConstraint("ck_branches_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Invited"),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    left_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.id);
                    table.CheckConstraint("ck_memberships_role", "role IN ('staff','coordinator','admin','safeguarding_officer','volunteer','client')");
                    table.CheckConstraint("ck_memberships_status", "status IN ('invited','active','suspended','left')");
                });

            migrationBuilder.CreateTable(
                name: "organization_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_key = table.Column<string>(type: "text", nullable: false),
                    policy_value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    legal_name = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    support_email = table.Column<string>(type: "text", nullable: true),
                    support_phone = table.Column<string>(type: "text", nullable: true),
                    branding = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xmin", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.CheckConstraint("ck_organizations_status", "status IN ('active','suspended','archived')");
                    table.CheckConstraint("ck_organizations_type", "type IN ('ngo','association','parish','company','other')");
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "text", nullable: false),
                    destination_hash = table.Column<string>(type: "text", nullable: false),
                    code_hash = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false, defaultValue: "Login"),
                    attempts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    max_attempts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)5),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ip_hash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_challenges", x => x.id);
                    table.CheckConstraint("ck_otp_attempts", "attempts <= max_attempts");
                    table.CheckConstraint("ck_otp_channel", "channel IN ('sms','email')");
                    table.CheckConstraint("ck_otp_purpose", "purpose IN ('login','registration','phone_change','email_change','recovery')");
                });

            migrationBuilder.CreateTable(
                name: "referral_directory",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_group = table.Column<string>(type: "text", nullable: false),
                    region_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    website = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    note_key = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_directory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    device_label = table.Column<string>(type: "text", nullable: true),
                    is_personal_device = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "senior_profiles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false, defaultValue: "AT"),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    mobility_note = table.Column<string>(type: "text", nullable: true),
                    living_situation = table.Column<string>(type: "text", nullable: true),
                    preferred_contact_method = table.Column<string>(type: "text", nullable: false, defaultValue: "App"),
                    vulnerability_flag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    vulnerability_set_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vulnerability_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_senior_profiles", x => x.id);
                    table.CheckConstraint("ck_senior_contact_method", "preferred_contact_method IN ('app','phone','sms','family')");
                    table.CheckConstraint("ck_senior_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
                    table.CheckConstraint("ck_senior_vulnerability_reason", "NOT vulnerability_flag OR vulnerability_reason IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    requires_verification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trust_level_snapshots",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    reason = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    computed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_level_snapshots", x => x.id);
                    table.CheckConstraint("ck_trust_level_range", "level BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "user_capabilities",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false, defaultValue: "Derived"),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_by_org_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_capabilities", x => x.id);
                    table.CheckConstraint("ck_user_capabilities_reason", "source = 'derived' OR reason IS NOT NULL");
                    table.CheckConstraint("ck_user_capabilities_source", "source IN ('derived','granted')");
                });

            migrationBuilder.CreateTable(
                name: "user_languages",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proficiency = table.Column<string>(type: "text", nullable: false, defaultValue: "B1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_languages", x => new { x.user_id, x.language_id });
                    table.CheckConstraint("ck_user_languages_proficiency", "proficiency IN ('a1','a2','b1','b2','c1','c2','native')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    email_verified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    phone_verified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    primary_auth_method = table.Column<string>(type: "text", nullable: false, defaultValue: "PhoneOtp"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    preferred_locale = table.Column<string>(type: "text", nullable: false, defaultValue: "de"),
                    senior_mode_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    last_login_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_auth_method", "primary_auth_method IN ('phone_otp','email_magic_link','password')");
                    table.CheckConstraint("ck_users_contact", "email IS NOT NULL OR phone IS NOT NULL");
                    table.CheckConstraint("ck_users_locale", "preferred_locale IN ('de','en','fa')");
                    table.CheckConstraint("ck_users_password_only_for_password_auth", "(primary_auth_method = 'password') = (password_hash IS NOT NULL)");
                    table.CheckConstraint("ck_users_phone_auth_needs_phone", "primary_auth_method <> 'phone_otp' OR phone IS NOT NULL");
                    table.CheckConstraint("ck_users_status", "status IN ('active','suspended','deactivated','deleted')");
                });

            migrationBuilder.CreateTable(
                name: "verifications",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NotStarted"),
                    provider = table.Column<string>(type: "text", nullable: false, defaultValue: "Manual"),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_by_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    valid_until_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    external_reference = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verifications", x => x.id);
                    table.CheckConstraint("ck_verifications_provider", "provider IN ('manual','organization','id_austria','kyc')");
                    table.CheckConstraint("ck_verifications_status", "status IN ('not_started','pending','submitted','verified','rejected','expired')");
                    table.CheckConstraint("ck_verifications_type", "type IN ('email','phone','identity','address','organization','training','background_check')");
                    table.CheckConstraint("ck_verifications_verified_needs_date", "status <> 'verified' OR verified_at_utc IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "volunteer_application_steps",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NotStarted"),
                    sla_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 14),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_application_steps", x => x.id);
                    table.CheckConstraint("ck_application_steps_status", "status IN ('not_started','in_progress','completed','blocked','skipped')");
                    table.CheckConstraint("ck_application_steps_step", "step IN ('interview','background_check','confidentiality_agreement','briefing','approval')");
                });

            migrationBuilder.CreateTable(
                name: "volunteer_applications",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Open"),
                    motivation = table.Column<string>(type: "text", nullable: true),
                    applied_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decline_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_applications", x => x.id);
                    table.CheckConstraint("ck_applications_decided_pair", "(decided_at_utc IS NULL) = (decided_by_user_id IS NULL)");
                    table.CheckConstraint("ck_applications_decline_reason", "status <> 'declined' OR decline_reason IS NOT NULL");
                    table.CheckConstraint("ck_applications_status", "status IN ('open','approved','declined','withdrawn')");
                });

            migrationBuilder.CreateTable(
                name: "volunteer_profiles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    max_distance_km = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    max_activities_per_week = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)3),
                    has_car = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_accepting_requests = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    reliability_score = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    active_since_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_profiles", x => x.id);
                    table.CheckConstraint("ck_volunteer_distance", "max_distance_km BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_volunteer_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
                    table.CheckConstraint("ck_volunteer_reliability", "reliability_score IS NULL OR reliability_score BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_volunteer_weekly", "max_activities_per_week BETWEEN 1 AND 40");
                });

            migrationBuilder.CreateTable(
                name: "volunteer_skills",
                schema: "public",
                columns: table => new
                {
                    volunteer_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    verified_by_org_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_skills", x => new { x.volunteer_profile_id, x.skill_id });
                });

            migrationBuilder.CreateTable(
                name: "user_interests",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interest_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_interests", x => new { x.user_id, x.interest_id });
                    table.ForeignKey(
                        name: "FK_user_interests_interests_interest_id",
                        column: x => x.interest_id,
                        principalSchema: "public",
                        principalTable: "interests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_interests_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_OrganizationId_OccurredOn",
                schema: "public",
                table: "activities",
                columns: new[] { "OrganizationId", "OccurredOn" },
                filter: "status = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_activities_VolunteerUserId_OccurredOn",
                schema: "public",
                table: "activities",
                columns: new[] { "VolunteerUserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_categories_code",
                schema: "public",
                table: "activity_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_action",
                schema: "public",
                table: "audit_entries",
                columns: new[] { "action", "at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_actor",
                schema: "public",
                table: "audit_entries",
                columns: new[] { "actor_user_id", "at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_subject",
                schema: "public",
                table: "audit_entries",
                columns: new[] { "subject_type", "subject_id", "at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_availability_user",
                schema: "public",
                table: "availability_slots",
                columns: new[] { "user_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_consents_user",
                schema: "public",
                table: "consents",
                columns: new[] { "user_id", "consent_type", "granted_at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_interests_code",
                schema: "public",
                table: "interests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_languages_iso_code",
                schema: "public",
                table: "languages",
                column: "iso_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_active",
                schema: "public",
                table: "otp_challenges",
                columns: new[] { "destination_hash", "created_at_utc" },
                descending: new[] { false, true },
                filter: "consumed_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_otp_cleanup",
                schema: "public",
                table: "otp_challenges",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_referral_directory_referral_group_region_code",
                schema: "public",
                table: "referral_directory",
                columns: new[] { "referral_group", "region_code" },
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user",
                schema: "public",
                table: "refresh_tokens",
                column: "user_id",
                filter: "revoked_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_hash",
                schema: "public",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_senior_profiles_geo",
                schema: "public",
                table: "senior_profiles",
                columns: new[] { "latitude", "longitude" },
                filter: "latitude IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_senior_profiles_postal",
                schema: "public",
                table: "senior_profiles",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_skills_code",
                schema: "public",
                table: "skills",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trust_snapshots_user",
                schema: "public",
                table: "trust_level_snapshots",
                columns: new[] { "user_id", "computed_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_capabilities_expiring",
                schema: "public",
                table: "user_capabilities",
                column: "expires_at_utc",
                filter: "expires_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_user_capabilities",
                schema: "public",
                table: "user_capabilities",
                columns: new[] { "user_id", "capability", "granted_by_org_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_interests_interest_id",
                schema: "public",
                table: "user_interests",
                column: "interest_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "public",
                table: "users",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ux_users_phone",
                schema: "public",
                table: "users",
                column: "phone",
                unique: true,
                filter: "phone IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_verifications_expiring",
                schema: "public",
                table: "verifications",
                column: "valid_until_utc",
                filter: "status = 'verified' AND valid_until_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_verifications_user_type",
                schema: "public",
                table: "verifications",
                columns: new[] { "user_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_volunteer_profiles_geo",
                schema: "public",
                table: "volunteer_profiles",
                columns: new[] { "latitude", "longitude" },
                filter: "latitude IS NOT NULL AND is_accepting_requests");

            migrationBuilder.Sql(@"
INSERT INTO interests (id, code, name_key, is_active) VALUES
    ('01917637-bf70-7601-8bf7-2c9bd4851214'::uuid, 'walking',      'interest.walking', true),
    ('01917637-bf70-7602-8bf7-2c9bd4851214'::uuid, 'gardening',    'interest.gardening', true),
    ('01917637-bf70-7603-8bf7-2c9bd4851214'::uuid, 'cooking',      'interest.cooking', true),
    ('01917637-bf70-7604-8bf7-2c9bd4851214'::uuid, 'cards_games',  'interest.cards_games', true),
    ('01917637-bf70-7605-8bf7-2c9bd4851214'::uuid, 'chess',        'interest.chess', true),
    ('01917637-bf70-7606-8bf7-2c9bd4851214'::uuid, 'reading',      'interest.reading', true),
    ('01917637-bf70-7607-8bf7-2c9bd4851214'::uuid, 'music',        'interest.music', true),
    ('01917637-bf70-7608-8bf7-2c9bd4851214'::uuid, 'handicraft',   'interest.handicraft', true),
    ('01917637-bf70-7609-8bf7-2c9bd4851214'::uuid, 'coffee_talk',  'interest.coffee_talk', true),
    ('01917637-bf70-760a-8bf7-2c9bd4851214'::uuid, 'language_exchange', 'interest.language_exchange', true),
    ('01917637-bf70-760b-8bf7-2c9bd4851214'::uuid, 'photography',  'interest.photography', true),
    ('01917637-bf70-760c-8bf7-2c9bd4851214'::uuid, 'church',       'interest.church', true),
    ('01917637-bf70-760d-8bf7-2c9bd4851214'::uuid, 'exercise',     'interest.exercise', true),
    ('01917637-bf70-760e-8bf7-2c9bd4851214'::uuid, 'animals',      'interest.animals', true)
ON CONFLICT (code) DO NOTHING;
");

            migrationBuilder.Sql(@"
INSERT INTO languages (id, iso_code, name_key) VALUES
    ('01917637-bf70-760f-8bf7-2c9bd4851214'::uuid, 'de','language.de'), 
    ('01917637-bf70-7610-8bf7-2c9bd4851214'::uuid, 'en','language.en'), 
    ('01917637-bf70-7611-8bf7-2c9bd4851214'::uuid, 'fa','language.fa'),
    ('01917637-bf70-7612-8bf7-2c9bd4851214'::uuid, 'tr','language.tr'), 
    ('01917637-bf70-7613-8bf7-2c9bd4851214'::uuid, 'bs','language.bs'), 
    ('01917637-bf70-7614-8bf7-2c9bd4851214'::uuid, 'hr','language.hr'),
    ('01917637-bf70-7615-8bf7-2c9bd4851214'::uuid, 'sr','language.sr'), 
    ('01917637-bf70-7616-8bf7-2c9bd4851214'::uuid, 'ar','language.ar'), 
    ('01917637-bf70-7617-8bf7-2c9bd4851214'::uuid, 'uk','language.uk'),
    ('01917637-bf70-7618-8bf7-2c9bd4851214'::uuid, 'ro','language.ro'), 
    ('01917637-bf70-7619-8bf7-2c9bd4851214'::uuid, 'hu','language.hu'), 
    ('01917637-bf70-761a-8bf7-2c9bd4851214'::uuid, 'it','language.it')
ON CONFLICT (iso_code) DO NOTHING;
");

            migrationBuilder.Sql(@"
INSERT INTO skills (id, code, name_key, requires_verification, is_active) VALUES
    ('01917637-bf70-761b-8bf7-2c9bd4851214'::uuid, 'first_aid',        'skill.first_aid',        true, true),
    ('01917637-bf70-761c-8bf7-2c9bd4851214'::uuid, 'driving',          'skill.driving',          true, true),
    ('01917637-bf70-761d-8bf7-2c9bd4851214'::uuid, 'elderly_support',  'skill.elderly_support',  false, true),
    ('01917637-bf70-761e-8bf7-2c9bd4851214'::uuid, 'shopping_help',    'skill.shopping_help',    false, true),
    ('01917637-bf70-761f-8bf7-2c9bd4851214'::uuid, 'tech_help',        'skill.tech_help',        false, true),
    ('01917637-bf70-7620-8bf7-2c9bd4851214'::uuid, 'paperwork_help',   'skill.paperwork_help',   false, true),
    ('01917637-bf70-7621-8bf7-2c9bd4851214'::uuid, 'conversation',     'skill.conversation',     false, true),
    ('01917637-bf70-7622-8bf7-2c9bd4851214'::uuid, 'gardening_help',   'skill.gardening_help',   false, true)
ON CONFLICT (code) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "activity_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "availability_slots",
                schema: "public");

            migrationBuilder.DropTable(
                name: "consents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "funder_memberships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "funders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "funding_relationships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "languages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organization_branches",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organization_memberships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organization_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "otp_challenges",
                schema: "public");

            migrationBuilder.DropTable(
                name: "referral_directory",
                schema: "public");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "senior_profiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "public");

            migrationBuilder.DropTable(
                name: "trust_level_snapshots",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_capabilities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_interests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_languages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "verifications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "volunteer_application_steps",
                schema: "public");

            migrationBuilder.DropTable(
                name: "volunteer_applications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "volunteer_profiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "volunteer_skills",
                schema: "public");

            migrationBuilder.DropTable(
                name: "interests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");
        }
    }
}
