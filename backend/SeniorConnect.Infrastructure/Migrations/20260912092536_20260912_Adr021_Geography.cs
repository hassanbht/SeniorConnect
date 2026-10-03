using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260912_Adr021_Geography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AustrianAdministrativeUnits",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundeslandCode = table.Column<string>(type: "text", nullable: false),
                    BundeslandName = table.Column<string>(type: "text", nullable: false),
                    BezirkCode = table.Column<string>(type: "text", nullable: false),
                    BezirkName = table.Column<string>(type: "text", nullable: false),
                    GemeindeCode = table.Column<string>(type: "text", nullable: false),
                    GemeindeName = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    LocalityName = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AustrianAdministrativeUnits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AustrianAdministrativeUnits",
                schema: "public");
        }
    }
}
