using BanterBotSports.DAL.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BanterBotSports.Tests.Unit;

public class AddPanelOrganizadorMigrationTests
{
    [Fact]
    public void Up_AddsTorneoPorcentajeOrganizador_AsNotNullable()
    {
        var migration = new AddPanelOrganizador();
        var builder = new MigrationBuilder("Npgsql");

        var up = typeof(AddPanelOrganizador).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        up.Should().NotBeNull();
        up!.Invoke(migration, new object[] { builder });

        var op = builder.Operations
            .OfType<AddColumnOperation>()
            .Single(o => o.Table == "Torneos" && o.Name == "PorcentajeOrganizador");

        op.IsNullable.Should().BeFalse("el porcentaje organizador del torneo debe persistirse como valor resuelto no nulo");
    }

    [Fact]
    public void TargetModel_DefinesTorneoPorcentajeOrganizador_AsNotNullable()
    {
        var migration = new AddPanelOrganizador();
        var modelBuilder = new ModelBuilder(new ConventionSet());

        var buildTargetModel = typeof(AddPanelOrganizador).GetMethod(
            "BuildTargetModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        buildTargetModel.Should().NotBeNull();
        buildTargetModel!.Invoke(migration, new object[] { modelBuilder });

        var property = modelBuilder.Model
            .FindEntityType("BanterBotSports.Entities.Torneo")
            ?.FindProperty("PorcentajeOrganizador");

        property.Should().NotBeNull("la metadata del diseñador debe coincidir con el esquema no nulo");
        property!.IsNullable.Should().BeFalse("el diseñador de migración debe modelar porcentaje organizador como requerido");
    }

    [Fact]
    public void TargetModel_IncludesIdentityRoleMapping_ForAspNetRolesSeed()
    {
        var migration = new AddPanelOrganizador();
        var modelBuilder = new ModelBuilder(new ConventionSet());

        var buildTargetModel = typeof(AddPanelOrganizador).GetMethod(
            "BuildTargetModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        buildTargetModel.Should().NotBeNull();
        buildTargetModel!.Invoke(migration, new object[] { modelBuilder });

        var roleEntity = modelBuilder.Model.FindEntityType("Microsoft.AspNetCore.Identity.IdentityRole");

        roleEntity.Should().NotBeNull("la migración inserta filas en AspNetRoles y el target model debe mapear IdentityRole");
        roleEntity!.GetTableName().Should().Be("AspNetRoles");
    }
}
