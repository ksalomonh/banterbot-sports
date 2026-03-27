using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BanterBotSports.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoUrlsToPartido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrlLocal",
                table: "Partidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrlVisitante",
                table: "Partidos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrlLocal",
                table: "Partidos");

            migrationBuilder.DropColumn(
                name: "LogoUrlVisitante",
                table: "Partidos");
        }
    }
}
