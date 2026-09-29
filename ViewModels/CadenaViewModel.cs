using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class RestauranteCadenaViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int TotalMesas { get; set; }
    public int TotalProductos { get; set; }
    public List<string> Admins { get; set; } = new();
}

public class CrearRestauranteViewModel
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email del admin es requerido")]
    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del admin es requerido")]
    [StringLength(100)]
    public string AdminNombre { get; set; } = string.Empty;

    // Solo si el admin es nuevo
    [DataType(DataType.Password)]
    public string? AdminPassword { get; set; }
}