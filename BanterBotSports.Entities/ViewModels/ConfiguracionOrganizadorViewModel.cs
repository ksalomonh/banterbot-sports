using System.ComponentModel.DataAnnotations;

namespace BanterBotSports.Entities.ViewModels;

public class ConfiguracionOrganizadorViewModel
{
    [Display(Name = "Porcentaje del organizador")]
    public decimal? PorcentajeOrganizadorGlobal { get; set; }

    public decimal PorcentajeMinimo { get; set; }
    public decimal PorcentajeMaximo { get; set; }
    public decimal PorcentajePlataforma { get; set; }
}
