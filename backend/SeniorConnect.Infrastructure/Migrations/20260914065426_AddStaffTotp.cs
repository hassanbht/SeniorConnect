using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTotp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "totp_enabled_at_utc",
                schema: "public",
                table: "users",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                schema: "public",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "totp_enabled_at_utc",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                schema: "public",
                table: "users");
        }
    }
}
