using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityEventOccurrenceCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventOccurrenceCancellations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceStartUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventOccurrenceCancellations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventOccurrenceCancellations",
                schema: "public");
        }
    }
}
