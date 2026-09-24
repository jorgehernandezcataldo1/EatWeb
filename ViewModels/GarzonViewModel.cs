using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class GarzonViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(100)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    // Obligatoria al crear; al editar, si viene vacía no se cambia
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public bool Activo { get; set; } = true;

    // Solo para el listado
    public List<int> Mesas { get; set; } = new();
}