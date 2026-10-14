using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class OperadorEstacionViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(100)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required]
    public string Estacion { get; set; } = "Cocina";

    public bool Activo { get; set; } = true;
}
