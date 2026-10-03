using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationSharingConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "location_sharing_consents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    consent_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_sharing_consents", x => x.id);
                    table.CheckConstraint("ck_location_sharing_level", "level IN ('None','Approximate','Precise')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_location_sharing_consents_history",
                schema: "public",
                table: "location_sharing_consents",
                columns: new[] { "user_id", "granted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_location_sharing_consents_current",
                schema: "public",
                table: "location_sharing_consents",
                column: "user_id",
                unique: true,
                filter: "revoked_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_sharing_consents",
                schema: "public");
        }
    }
}
