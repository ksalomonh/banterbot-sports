namespace BanterBotSports.Entities;

public class ConfiguracionGlobal
{
    public int Id { get; set; }                           // Always 1
    public decimal PorcentajePlataforma { get; set; }     // Default 10
    public decimal PorcentajeOrganizadorMin { get; set; } // Default 5
    public decimal PorcentajeOrganizadorMax { get; set; } // Default 30
    public decimal MontoInscripcionMinimo { get; set; }   // Default 500
}
