namespace BanterBotSports.BL.Models;

/// <summary>Ranking entry used as input to prize distribution calculation.</summary>
public record RankingParticipante(
    int ParticipanteId,
    string NombreDisplay,
    int PuntosTotal,
    int Posicion);
