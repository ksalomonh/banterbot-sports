using System.ComponentModel.DataAnnotations;

namespace BanterBotSports.Entities.ViewModels;

public record ProfileEditViewModel
{
    [Required(ErrorMessage = "El nombre de jugador es obligatorio")]
    [MinLength(2, ErrorMessage = "Mínimo 2 caracteres")]
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres")]
    public required string NombreDisplay { get; init; }
}
