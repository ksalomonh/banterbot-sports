using BanterBotSports.DAL.Repositories.Interfaces;

namespace BanterBotSports.DAL;

/// <summary>
/// Wraps AppDbContext.SaveChangesAsync behind the IUnitOfWork interface
/// so BL services are not coupled to EF Core.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
