using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for ChatRepository using an in-memory database.
/// Tests: Add, GetByTorneo ordered desc, limit, and beforeId cursor pagination.
/// </summary>
public class ChatRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ChatRepository _sut;

    public ChatRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new ChatRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MensajeChat BuildMensaje(int torneoId, DateTimeOffset fecha, string contenido = "hola")
        => new()
        {
            TorneoId = torneoId,
            UserId = "user1",
            Contenido = contenido,
            FechaUtc = fecha,
            TipoMensaje = TipoMensajeChat.Normal,
            NombreDisplay = "Player One"
        };

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_StagesMensajeForSaveAndReturnsIt()
    {
        // Arrange
        var mensaje = BuildMensaje(torneoId: 1, fecha: DateTimeOffset.UtcNow);

        // Act
        var result = await _sut.AddAsync(mensaje);
        await _context.SaveChangesAsync(); // caller (service/UoW) is responsible for commit

        // Assert
        result.Id.Should().BeGreaterThan(0);
        _context.MensajesChat.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByTorneoAsync_ReturnsMessagesOrderedByFechaDescending()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        _context.MensajesChat.AddRange(
            BuildMensaje(torneoId: 1, fecha: now.AddMinutes(-10), contenido: "primero"),
            BuildMensaje(torneoId: 1, fecha: now.AddMinutes(-5), contenido: "segundo"),
            BuildMensaje(torneoId: 1, fecha: now, contenido: "tercero")
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByTorneoAsync(torneoId: 1, limit: 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].Contenido.Should().Be("tercero"); // most recent first
        result[1].Contenido.Should().Be("segundo");
        result[2].Contenido.Should().Be("primero");
    }

    [Fact]
    public async Task GetByTorneoAsync_RespectsLimit()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            _context.MensajesChat.Add(BuildMensaje(torneoId: 1, fecha: now.AddMinutes(-i)));
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByTorneoAsync(torneoId: 1, limit: 3);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByTorneoAsync_WithBeforeId_ReturnsCursorPaginatedResults()
    {
        // Arrange — add 5 messages for torneo 1
        var now = DateTimeOffset.UtcNow;
        for (int i = 1; i <= 5; i++)
        {
            _context.MensajesChat.Add(BuildMensaje(torneoId: 1, fecha: now.AddMinutes(-i)));
        }
        await _context.SaveChangesAsync();

        // Get all to find a pivot Id
        var all = await _sut.GetByTorneoAsync(torneoId: 1, limit: 10);
        var pivotId = all[2].Id; // 3rd message in desc order

        // Act — only want messages older than pivotId
        var result = await _sut.GetByTorneoAsync(torneoId: 1, limit: 10, beforeId: pivotId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.Id < pivotId);
    }

    [Fact]
    public async Task GetByTorneoAsync_DoesNotReturnMessagesFromOtherTorneos()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        _context.MensajesChat.AddRange(
            BuildMensaje(torneoId: 1, fecha: now, contenido: "torneo 1"),
            BuildMensaje(torneoId: 2, fecha: now, contenido: "torneo 2")
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByTorneoAsync(torneoId: 1, limit: 10);

        // Assert
        result.Should().HaveCount(1);
        result[0].Contenido.Should().Be("torneo 1");
    }
}
