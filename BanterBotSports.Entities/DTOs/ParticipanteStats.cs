namespace BanterBotSports.Entities.DTOs;

public record PrediccionConResultado(
    string Equipo1,
    string Equipo2,
    int GolesPredichos1,
    int GolesPredichos2,
    int? GolesOficiales1,
    int? GolesOficiales2,
    int? PuntosObtenidos
);

public record ParticipanteStats(
    string NombreParticipante,
    string NombreTorneo,
    int NumeroJornada,
    int PosicionRanking,
    int PuntosTotal,
    IReadOnlyList<PrediccionConResultado> Predicciones
);
