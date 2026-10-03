using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowDisputeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoShowDisputeReason",
                schema: "public",
                table: "HelpRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NoShowDisputedAtUtc",
                schema: "public",
                table: "HelpRequests",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NoShowDisputedByUserId",
                schema: "public",
                table: "HelpRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreNoShowReliabilityScore",
                schema: "public",
                table: "HelpRequests",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoShowDisputeReason",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "NoShowDisputedAtUtc",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "NoShowDisputedByUserId",
                schema: "public",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "PreNoShowReliabilityScore",
                schema: "public",
                table: "HelpRequests");
        }
    }
}
