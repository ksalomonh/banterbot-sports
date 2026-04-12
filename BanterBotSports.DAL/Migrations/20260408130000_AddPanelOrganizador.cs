using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BanterBotSports.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPanelOrganizador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeOrganizador",
                table: "Torneos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeOrganizadorGlobal",
                table: "AspNetUsers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Seed Organizador role
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[] { "c3d4e5f6-a7b8-9012-cdef-123456789012", "Organizador", "ORGANIZADOR", "c3d4e5f6-a7b8-9012-cdef-123456789012" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "c3d4e5f6-a7b8-9012-cdef-123456789012");

            migrationBuilder.DropColumn(
                name: "PorcentajeOrganizador",
                table: "Torneos");

            migrationBuilder.DropColumn(
                name: "PorcentajeOrganizadorGlobal",
                table: "AspNetUsers");
        }
    }
}
