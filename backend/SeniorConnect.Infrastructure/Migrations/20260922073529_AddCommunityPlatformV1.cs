using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPlatformV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_community_groups_org_archived",
                schema: "public",
                table: "CommunityGroups");

            // Title -> Name, Scope/JoinPolicy -> Visibility and IsArchived -> Status are NOT
            // straight renames: the old and new enums have different member orders, so a
            // column rename would silently reinterpret existing integer values under new
            // semantics (e.g. old GroupVisibilityScope.LocalNeighborhood = 1 would read
            // back as the unrelated new GroupVisibility.Private = 1). The new columns are
            // added first, existing rows are translated explicitly (further down), and only
            // then are the old columns dropped — no group loses its name or its privacy.

            // LocationPostalCode -> PostalCode is a genuine rename (same meaning, same type).
            migrationBuilder.RenameColumn(
                name: "LocationPostalCode",
                schema: "public",
                table: "CommunityGroups",
                newName: "PostalCode");

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BanReason",
                schema: "public",
                table: "GroupMemberships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationToken",
                schema: "public",
                table: "GroupMemberships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                schema: "public",
                table: "GroupMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MutedUntilUtc",
                schema: "public",
                table: "GroupMemberships",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAtUtc",
                schema: "public",
                table: "GroupMemberships",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatorType",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "public",
                table: "CommunityGroups",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "public",
                table: "CommunityGroups",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryLanguageCode",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Old enums: GroupVisibilityScope { Public 0, LocalNeighborhood 1, OrganizationMembers 2, PrivateInvitation 3 },
            //            GroupJoinPolicy      { Open 0, RequestApproval 1, InviteOnly 2 }.
            // New enums: GroupVisibility      { Public 0, Private 1, OrganizationPrivate 2 },
            //            GroupStatus          { Draft 0, Active 1, Paused 2, Archived 3, Blocked 4 },
            //            CreatorType          { Individual 0, ..., VerifiedOrganization 3 },
            //            GroupVerificationStatus { Unverified 0, OrganizationVerified 1 }.
            // Anything that was not open to everyone stays non-public (new Public = direct join).
            // Pre-V1 groups could only be published by organizations (ADR-020).
            migrationBuilder.Sql("""
                UPDATE "public"."CommunityGroups" SET
                    "Name" = "Title",
                    "Visibility" = CASE
                        WHEN "Scope" = 2 AND "OrganizationId" IS NOT NULL THEN 2
                        WHEN "Scope" IN (2, 3) OR "JoinPolicy" IN (1, 2) THEN 1
                        ELSE 0
                    END,
                    "Status" = CASE WHEN "IsArchived" THEN 3 ELSE 1 END,
                    "CreatorType" = CASE WHEN "OrganizationId" IS NOT NULL THEN 3 ELSE 0 END,
                    "VerificationStatus" = CASE WHEN "OrganizationId" IS NOT NULL THEN 1 ELSE 0 END;
                """);

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "JoinPolicy",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.CreateTable(
                name: "CommunityCategories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    NameKey = table.Column<string>(type: "text", nullable: false),
                    DescriptionKey = table.Column<string>(type: "text", nullable: true),
                    GroupType = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityComments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModerationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityInvitations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityModerationActions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    IsPlatformAction = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityModerationActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPosts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModerationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityReports",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolutionNote = table.Column<string>(type: "text", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_language",
                schema: "public",
                table: "CommunityGroups",
                column: "PrimaryLanguageCode");

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_org_status",
                schema: "public",
                table: "CommunityGroups",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_org_verification",
                schema: "public",
                table: "CommunityGroups",
                columns: new[] { "OrganizationId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_status_visibility_city",
                schema: "public",
                table: "CommunityGroups",
                columns: new[] { "Status", "Visibility", "City" });

            migrationBuilder.CreateIndex(
                name: "ix_community_categories_code",
                schema: "public",
                table: "CommunityCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_comments_post_status",
                schema: "public",
                table: "CommunityComments",
                columns: new[] { "PostId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_invitations_group_status",
                schema: "public",
                table: "CommunityInvitations",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_invitations_token",
                schema: "public",
                table: "CommunityInvitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_moderation_actions_group_action",
                schema: "public",
                table: "CommunityModerationActions",
                columns: new[] { "GroupId", "Action" });

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_group_status_created",
                schema: "public",
                table: "CommunityPosts",
                columns: new[] { "GroupId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_community_reports_reporter_status",
                schema: "public",
                table: "CommunityReports",
                columns: new[] { "ReporterUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_reports_target",
                schema: "public",
                table: "CommunityReports",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityCategories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityComments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityInvitations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityModerationActions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityPosts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CommunityReports",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_language",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_org_status",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_org_verification",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_status_visibility_city",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "BanReason",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropColumn(
                name: "InvitationToken",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropColumn(
                name: "MutedUntilUtc",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropColumn(
                name: "RequestedAtUtc",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "public",
                table: "CommunityGroups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JoinPolicy",
                schema: "public",
                table: "CommunityGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "public",
                table: "CommunityGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Reverse of the Up() translation — keep names, privacy and archive state.
            migrationBuilder.Sql("""
                UPDATE "public"."CommunityGroups" SET
                    "Title" = "Name",
                    "Scope" = CASE "Visibility" WHEN 2 THEN 2 WHEN 1 THEN 3 ELSE 0 END,
                    "JoinPolicy" = CASE WHEN "Visibility" = 0 THEN 0 ELSE 1 END,
                    "IsArchived" = ("Status" IN (3, 4));
                """);

            migrationBuilder.DropColumn(
                name: "City",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "CreatorType",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "PrimaryLanguageCode",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropColumn(
                name: "Rules",
                schema: "public",
                table: "CommunityGroups");

            // PostalCode -> LocationPostalCode mirrors the genuine rename from Up().
            migrationBuilder.RenameColumn(
                name: "PostalCode",
                schema: "public",
                table: "CommunityGroups",
                newName: "LocationPostalCode");

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_org_archived",
                schema: "public",
                table: "CommunityGroups",
                columns: new[] { "OrganizationId", "IsArchived" });
        }
    }
}
