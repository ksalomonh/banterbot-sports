using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BanterBotSports.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramChatIdToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            // Data migration: copy existing PhoneNumber (which stores Telegram Chat IDs) into the new column
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "TelegramChatId" = "PhoneNumber",
                    "PhoneNumber" = NULL
                WHERE "PhoneNumber" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore: copy TelegramChatId back to PhoneNumber before dropping the column
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "PhoneNumber" = "TelegramChatId"
                WHERE "TelegramChatId" IS NOT NULL AND "PhoneNumber" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "AspNetUsers");
        }
    }
}
