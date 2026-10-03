using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260912_Adr021_Multiprovider_Auth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.EnsureSchema(
                name: "safeguarding");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastMonthlyReminderMonth",
                schema: "public",
                table: "volunteer_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "access_logs",
                schema: "safeguarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accessed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    accessed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AccountDeletionRequests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfirmationToken = table.Column<string>(type: "text", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Tier1ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ScheduledTier2PurgeUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Tier2ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuddyAssignments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level3PlusCompletedCount = table.Column<int>(type: "integer", nullable: false),
                    IsBuddyRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedBuddyVolunteerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsWaived = table.Column<bool>(type: "boolean", nullable: false),
                    WaiverReason = table.Column<string>(type: "text", nullable: true),
                    WaivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WaivedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuddyAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_notes",
                schema: "safeguarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_officer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_text = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "safeguarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_officer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_notes = table.Column<string>(type: "text", nullable: true),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityEvents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    LocationAddress = table.Column<string>(type: "text", nullable: true),
                    LocationPostalCode = table.Column<string>(type: "text", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RecurrenceFrequency = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceUntilUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityGroups",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    JoinPolicy = table.Column<int>(type: "integer", nullable: false),
                    LocationPostalCode = table.Column<string>(type: "text", nullable: true),
                    MaxMembers = table.Column<int>(type: "integer", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    used_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "EventRegistrations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WaitlistPosition = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseRecords",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountGiven = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountSpent = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountReturned = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ReceiptNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisputeReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamilyRelationships",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitationCode = table.Column<string>(type: "text", nullable: true),
                    InvitationExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpRequests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredSafetyLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiredTrustLevel = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ScheduledEndUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    LocationType = table.Column<int>(type: "integer", nullable: false),
                    LocationAddress = table.Column<string>(type: "text", nullable: true),
                    LocationPostalCode = table.Column<string>(type: "text", nullable: true),
                    LocationCity = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TransportMode = table.Column<int>(type: "integer", nullable: false),
                    InsuranceContext = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedVolunteerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OfferedVolunteersJson = table.Column<string>(type: "text", nullable: false),
                    OfferTier = table.Column<int>(type: "integer", nullable: false),
                    TierAdvancedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    EscalatedToCoordinatorAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Reminder24hSentAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Reminder2hSentAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CheckedInAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancellationReasonCode = table.Column<int>(type: "integer", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RowVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xmin", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpRequestStatusHistories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HelpRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpRequestStatusHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeyCustodies",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    KeyTag = table.Column<string>(type: "text", nullable: false),
                    HandedOverAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ExpectedReturnAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyCustodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageThreads",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextType = table.Column<int>(type: "integer", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationBudgetTrackers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    NonUrgentCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationBudgetTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationMessages",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    QuietHoursEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    QuietHoursStart = table.Column<TimeSpan>(type: "interval", nullable: false),
                    QuietHoursEnd = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HelpRequestsCategoryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CommunityCategoryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FamilyWelfareCategoryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SystemAccountCategoryEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyAlerts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeniorAccessLogs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessedByUserName = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ResourceAccessed = table.Column<string>(type: "text", nullable: false),
                    PlainLanguageDescription = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IpAddressHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeniorAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreadMessages",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsFlaggedForModeration = table.Column<bool>(type: "boolean", nullable: false),
                    ModerationReason = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrustedContacts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Relationship = table.Column<string>(type: "text", nullable: false),
                    IsPrimaryEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnSafetyAlert = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_external_logins",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    linked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_logins", x => x.id);
                    table.CheckConstraint("ck_user_external_logins_provider", "provider IN ('google','id_austria')");
                });

            migrationBuilder.CreateTable(
                name: "UserBlocks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zugangskarten",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeniorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByCaregiverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairingCode = table.Column<string>(type: "text", nullable: false),
                    QrPayload = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zugangskarten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamilyPermissions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionType = table.Column<int>(type: "integer", nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyPermissions_FamilyRelationships_FamilyRelationshipId",
                        column: x => x.FamilyRelationshipId,
                        principalSchema: "public",
                        principalTable: "FamilyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users",
                sql: "primary_auth_method IN ('phone_otp','email_magic_link','password','google','id_austria','email_password')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges",
                sql: "purpose IN ('login','registration','phone_change','email_change','recovery','phone_verification')");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user",
                schema: "public",
                table: "email_verification_tokens",
                column: "user_id",
                filter: "used_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyPermissions_FamilyRelationshipId",
                schema: "public",
                table: "FamilyPermissions",
                column: "FamilyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "ix_user_external_logins_user",
                schema: "public",
                table: "user_external_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_external_logins_provider_key",
                schema: "public",
                table: "user_external_logins",
                columns: new[] { "provider", "provider_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_logs",
                schema: "safeguarding");

            migrationBuilder.DropTable(
                name: "AccountDeletionRequests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "BuddyAssignments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "case_notes",
                schema: "safeguarding");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "safeguarding");

            migrationBuilder.DropTable(
                name: "CommunityEvents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityGroups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "email_verification_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "EventRegistrations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ExpenseRecords",
                schema: "public");

            migrationBuilder.DropTable(
                name: "FamilyPermissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "GroupMemberships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "HelpRequests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "HelpRequestStatusHistories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "KeyCustodies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MessageThreads",
                schema: "public");

            migrationBuilder.DropTable(
                name: "NotificationBudgetTrackers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "NotificationMessages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SafetyAlerts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SeniorAccessLogs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ThreadMessages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TrustedContacts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_external_logins",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserBlocks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Zugangskarten",
                schema: "public");

            migrationBuilder.DropTable(
                name: "FamilyRelationships",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges");

            migrationBuilder.DropColumn(
                name: "LastMonthlyReminderMonth",
                schema: "public",
                table: "volunteer_profiles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_auth_method",
                schema: "public",
                table: "users",
                sql: "primary_auth_method IN ('phone_otp','email_magic_link','password')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_otp_purpose",
                schema: "public",
                table: "otp_challenges",
                sql: "purpose IN ('login','registration','phone_change','email_change','recovery')");
        }
    }
}
