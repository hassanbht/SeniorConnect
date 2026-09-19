using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceAndLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_volunteer_profiles_user_id",
                schema: "public",
                table: "volunteer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_senior_deleted",
                schema: "public",
                table: "TrustedContacts",
                columns: new[] { "SeniorUserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "ix_support_profiles_user_id",
                schema: "public",
                table: "support_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_senior_access_logs_senior_time",
                schema: "public",
                table: "SeniorAccessLogs",
                columns: new[] { "SeniorUserId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_safety_alerts_senior_status",
                schema: "public",
                table: "SafetyAlerts",
                columns: new[] { "SeniorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id",
                schema: "public",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_messages_recipient_status_created",
                schema: "public",
                table: "NotificationMessages",
                columns: new[] { "RecipientUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_help_request_history_request_time",
                schema: "public",
                table: "HelpRequestStatusHistories",
                columns: new[] { "HelpRequestId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_help_requests_category_id",
                schema: "public",
                table: "HelpRequests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "ix_help_requests_org_status",
                schema: "public",
                table: "HelpRequests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_help_requests_senior_status",
                schema: "public",
                table: "HelpRequests",
                columns: new[] { "SeniorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_help_requests_status_scheduled",
                schema: "public",
                table: "HelpRequests",
                columns: new[] { "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_help_requests_volunteer_status",
                schema: "public",
                table: "HelpRequests",
                columns: new[] { "AssignedVolunteerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_group_memberships_group_status",
                schema: "public",
                table: "GroupMemberships",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_group_memberships_user_status",
                schema: "public",
                table: "GroupMemberships",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_family_relationships_caregiver_status",
                schema: "public",
                table: "FamilyRelationships",
                columns: new[] { "CaregiverUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_family_relationships_senior_status",
                schema: "public",
                table: "FamilyRelationships",
                columns: new[] { "SeniorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_event_registrations_event_status",
                schema: "public",
                table: "EventRegistrations",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_event_registrations_user_status",
                schema: "public",
                table: "EventRegistrations",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_category",
                schema: "public",
                table: "CommunityGroups",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_community_groups_org_archived",
                schema: "public",
                table: "CommunityGroups",
                columns: new[] { "OrganizationId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "ix_community_events_group",
                schema: "public",
                table: "CommunityEvents",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "ix_community_events_org",
                schema: "public",
                table: "CommunityEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_events_schedule",
                schema: "public",
                table: "CommunityEvents",
                columns: new[] { "StartsAtUtc", "IsCancelled" });

            migrationBuilder.CreateIndex(
                name: "ix_austria_bezirk",
                schema: "public",
                table: "AustrianAdministrativeUnits",
                column: "BezirkCode");

            migrationBuilder.CreateIndex(
                name: "ix_austria_bundesland",
                schema: "public",
                table: "AustrianAdministrativeUnits",
                column: "BundeslandCode");

            migrationBuilder.CreateIndex(
                name: "ix_austria_geo_coords",
                schema: "public",
                table: "AustrianAdministrativeUnits",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_austria_geo_gemeinde",
                schema: "public",
                table: "AustrianAdministrativeUnits",
                column: "GemeindeName");

            migrationBuilder.CreateIndex(
                name: "ix_austria_geo_plz",
                schema: "public",
                table: "AustrianAdministrativeUnits",
                column: "PostalCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_volunteer_profiles_user_id",
                schema: "public",
                table: "volunteer_profiles");

            migrationBuilder.DropIndex(
                name: "ix_trusted_contacts_senior_deleted",
                schema: "public",
                table: "TrustedContacts");

            migrationBuilder.DropIndex(
                name: "ix_support_profiles_user_id",
                schema: "public",
                table: "support_profiles");

            migrationBuilder.DropIndex(
                name: "ix_senior_access_logs_senior_time",
                schema: "public",
                table: "SeniorAccessLogs");

            migrationBuilder.DropIndex(
                name: "ix_safety_alerts_senior_status",
                schema: "public",
                table: "SafetyAlerts");

            migrationBuilder.DropIndex(
                name: "ix_notification_preferences_user_id",
                schema: "public",
                table: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "ix_notification_messages_recipient_status_created",
                schema: "public",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "ix_help_request_history_request_time",
                schema: "public",
                table: "HelpRequestStatusHistories");

            migrationBuilder.DropIndex(
                name: "ix_help_requests_category_id",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "ix_help_requests_org_status",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "ix_help_requests_senior_status",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "ix_help_requests_status_scheduled",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "ix_help_requests_volunteer_status",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "ix_group_memberships_group_status",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropIndex(
                name: "ix_group_memberships_user_status",
                schema: "public",
                table: "GroupMemberships");

            migrationBuilder.DropIndex(
                name: "ix_family_relationships_caregiver_status",
                schema: "public",
                table: "FamilyRelationships");

            migrationBuilder.DropIndex(
                name: "ix_family_relationships_senior_status",
                schema: "public",
                table: "FamilyRelationships");

            migrationBuilder.DropIndex(
                name: "ix_event_registrations_event_status",
                schema: "public",
                table: "EventRegistrations");

            migrationBuilder.DropIndex(
                name: "ix_event_registrations_user_status",
                schema: "public",
                table: "EventRegistrations");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_category",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropIndex(
                name: "ix_community_groups_org_archived",
                schema: "public",
                table: "CommunityGroups");

            migrationBuilder.DropIndex(
                name: "ix_community_events_group",
                schema: "public",
                table: "CommunityEvents");

            migrationBuilder.DropIndex(
                name: "ix_community_events_org",
                schema: "public",
                table: "CommunityEvents");

            migrationBuilder.DropIndex(
                name: "ix_community_events_schedule",
                schema: "public",
                table: "CommunityEvents");

            migrationBuilder.DropIndex(
                name: "ix_austria_bezirk",
                schema: "public",
                table: "AustrianAdministrativeUnits");

            migrationBuilder.DropIndex(
                name: "ix_austria_bundesland",
                schema: "public",
                table: "AustrianAdministrativeUnits");

            migrationBuilder.DropIndex(
                name: "ix_austria_geo_coords",
                schema: "public",
                table: "AustrianAdministrativeUnits");

            migrationBuilder.DropIndex(
                name: "ix_austria_geo_gemeinde",
                schema: "public",
                table: "AustrianAdministrativeUnits");

            migrationBuilder.DropIndex(
                name: "ix_austria_geo_plz",
                schema: "public",
                table: "AustrianAdministrativeUnits");
        }
    }
}
