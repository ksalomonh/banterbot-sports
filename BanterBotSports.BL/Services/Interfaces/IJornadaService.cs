namespace BanterBotSports.BL.Services.Interfaces;

public interface IJornadaService
{
    Task AbrirJornadaAsync(int jornadaId);
    Task CerrarJornadaAsync(int jornadaId);
    Task FinalizarJornadaAsync(int jornadaId);
}
