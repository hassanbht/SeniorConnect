using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSafeguardingOfficerRoleCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships",
                sql: "role IN ('Staff','Coordinator','Admin','SafeguardingOfficer','PlatformSafeguardingOfficer','Volunteer','Client')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                schema: "public",
                table: "organization_memberships",
                sql: "role IN ('Staff','Coordinator','Admin','SafeguardingOfficer','Volunteer','Client')");
        }
    }
}
