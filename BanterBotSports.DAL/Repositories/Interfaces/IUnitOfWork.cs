namespace BanterBotSports.DAL.Repositories.Interfaces;

/// <summary>
/// Abstraction over EF Core's SaveChangesAsync so BL services can persist
/// changes without taking a direct dependency on AppDbContext.
/// </summary>
public interface IUnitOfWork
{
    Task SaveAsync(CancellationToken cancellationToken = default);
}
