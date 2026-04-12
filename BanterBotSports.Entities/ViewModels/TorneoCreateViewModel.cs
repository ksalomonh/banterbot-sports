using System.ComponentModel.DataAnnotations;

namespace BanterBotSports.Entities.ViewModels;

public class TorneoCreateViewModel
{
    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [Range(1, 100)]
    public int NumJornadas { get; set; }

    [Required]
    [Range(0, 100000)]
    public decimal MontoInscripcion { get; set; }

    [Required]
    [Range(0, 100)]
    public int PtosResultado { get; set; }

    [Required]
    [Range(0, 100)]
    public int PtosMarcador { get; set; }

    [Required]
    [Range(0, 100)]
    public int PtosGolesJornada { get; set; }

    [Range(0, 100)]
    public decimal? PorcentajeOrganizador { get; set; }

    public IList<ConfiguracionPremioViewModel> ConfiguracionPremios { get; set; } = new List<ConfiguracionPremioViewModel>();

    public IList<string> PartidosSeleccionados { get; set; } = new List<string>();
}

public class ConfiguracionPremioViewModel
{
    [Required]
    [Range(1, 100)]
    public int Posicion { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal Porcentaje { get; set; }
}
