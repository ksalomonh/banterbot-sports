using System.ComponentModel.DataAnnotations;

namespace BanterBotSports.Entities.ViewModels;

public class RegisterViewModel
{
    [Required]
    [MaxLength(100)]
    [Display(Name = "Nombre")]
    public string NombreDisplay { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
