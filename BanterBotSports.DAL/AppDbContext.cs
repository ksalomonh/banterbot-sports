using BanterBotSports.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Torneo> Torneos => Set<Torneo>();
    public DbSet<ConfiguracionPremio> ConfiguracionesPremio => Set<ConfiguracionPremio>();
    public DbSet<Jornada> Jornadas => Set<Jornada>();
    public DbSet<Partido> Partidos => Set<Partido>();
    public DbSet<Participante> Participantes => Set<Participante>();
    public DbSet<UsuarioTelegram> UsuariosTelegram => Set<UsuarioTelegram>();
    public DbSet<PrediccionPartido> PrediccionesPartido => Set<PrediccionPartido>();
    public DbSet<PrediccionJornada> PrediccionesJornada => Set<PrediccionJornada>();
    public DbSet<MensajeChat> MensajesChat => Set<MensajeChat>();
    public DbSet<ConfiguracionGlobal> ConfiguracionGlobal => Set<ConfiguracionGlobal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Decimal precision
        builder.Entity<Torneo>()
            .Property(t => t.MontoInscripcion)
            .HasPrecision(18, 2);

        builder.Entity<ConfiguracionPremio>()
            .Property(c => c.Porcentaje)
            .HasPrecision(18, 2);

        builder.Entity<ConfiguracionGlobal>()
            .Property(c => c.PorcentajePlataforma)
            .HasPrecision(18, 2);

        builder.Entity<ConfiguracionGlobal>()
            .Property(c => c.PorcentajeOrganizadorMin)
            .HasPrecision(18, 2);

        builder.Entity<ConfiguracionGlobal>()
            .Property(c => c.PorcentajeOrganizadorMax)
            .HasPrecision(18, 2);

        builder.Entity<ConfiguracionGlobal>()
            .Property(c => c.MontoInscripcionMinimo)
            .HasPrecision(18, 2);

        // Unique index: one Telegram account per user
        builder.Entity<UsuarioTelegram>()
            .HasIndex(u => u.TelegramUserId)
            .IsUnique();

        // Composite unique: one participation per (torneo, user)
        builder.Entity<Participante>()
            .HasIndex(p => new { p.TorneoId, p.UserId })
            .IsUnique();

        // Composite unique: one prediction per (partido, participante)
        builder.Entity<PrediccionPartido>()
            .HasIndex(pp => new { pp.PartidoId, pp.ParticipanteId })
            .IsUnique();

        // Composite unique: one jornada prediction per (jornada, participante)
        builder.Entity<PrediccionJornada>()
            .HasIndex(pj => new { pj.JornadaId, pj.ParticipanteId })
            .IsUnique();

        // MensajeChat: max 500 chars on Contenido, composite index for pagination
        builder.Entity<MensajeChat>()
            .Property(m => m.Contenido)
            .HasMaxLength(500);

        builder.Entity<MensajeChat>()
            .HasIndex(m => new { m.TorneoId, m.FechaUtc });
    }
}
