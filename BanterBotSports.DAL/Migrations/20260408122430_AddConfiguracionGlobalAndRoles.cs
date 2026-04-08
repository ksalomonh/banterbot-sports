using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BanterBotSports.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracionGlobalAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionGlobal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PorcentajePlataforma = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PorcentajeOrganizadorMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PorcentajeOrganizadorMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoInscripcionMinimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionGlobal", x => x.Id);
                });

            // Seed default configuration row
            migrationBuilder.InsertData(
                table: "ConfiguracionGlobal",
                columns: new[] { "Id", "PorcentajePlataforma", "PorcentajeOrganizadorMin", "PorcentajeOrganizadorMax", "MontoInscripcionMinimo" },
                values: new object[] { 1, 10m, 5m, 30m, 500m });

            // Seed Admin and Jugador roles
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[,]
                {
                    { "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "Admin", "ADMIN", "a1b2c3d4-e5f6-7890-abcd-ef1234567890" },
                    { "b2c3d4e5-f6a7-8901-bcde-f12345678901", "Jugador", "JUGADOR", "b2c3d4e5-f6a7-8901-bcde-f12345678901" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValues: new object[] { "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "b2c3d4e5-f6a7-8901-bcde-f12345678901" });

            migrationBuilder.DropTable(
                name: "ConfiguracionGlobal");
        }
    }
}
