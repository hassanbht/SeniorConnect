using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeniorConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePhotoAndInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_interests_interests_interest_id",
                schema: "public",
                table: "user_interests");

            migrationBuilder.DropForeignKey(
                name: "FK_user_interests_users_user_id",
                schema: "public",
                table: "user_interests");

            migrationBuilder.DropIndex(
                name: "IX_user_interests_interest_id",
                schema: "public",
                table: "user_interests");

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                schema: "public",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_url",
                schema: "public",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_user_interests_interest_id",
                schema: "public",
                table: "user_interests",
                column: "interest_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_interests_interests_interest_id",
                schema: "public",
                table: "user_interests",
                column: "interest_id",
                principalSchema: "public",
                principalTable: "interests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_interests_users_user_id",
                schema: "public",
                table: "user_interests",
                column: "user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
