using BanterBotSports.DAL;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BanterBotSports.Tests.Unit;

public class AppDbContextMigrationConsistencyTests
{
    [Fact]
    public void Model_DoesNotHavePendingMigrationChanges()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BanterBotSports.Web"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
            .Options;

        using var context = new AppDbContext(options);

        context.Database.HasPendingModelChanges().Should().BeFalse(
            "la suite de integración ejecuta MigrateAsync al iniciar y no debe detectar drift de snapshot/modelo");
    }
}
