using System.ComponentModel.DataAnnotations;

namespace BanterBotSports.Entities.ViewModels;

public record RegisterViewModel
{
    [Required]
    [MaxLength(100)]
    [Display(Name = "Nombre")]
    public string NombreDisplay { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [Phone(ErrorMessage = "Formato de teléfono inválido.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email (solo para recuperación de contraseña)")]
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
