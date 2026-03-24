namespace BanterBotSports.Entities;

public class ConfiguracionPremio
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public int Posicion { get; set; }
    public decimal Porcentaje { get; set; }

    public Torneo Torneo { get; set; } = null!;
}
