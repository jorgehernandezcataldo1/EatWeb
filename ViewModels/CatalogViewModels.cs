using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class CategoriaViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(80)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El orden es requerido")]
    public int Orden { get; set; }

    public bool Activa { get; set; } = true;
}

public class IngredienteViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(80)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}

public class ProductoViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "El precio es requerido")]
    [Range(0.01, 999999)]
    public decimal Precio { get; set; }

    [StringLength(300)]
    public string? ImagenUrl { get; set; }

    [Required(ErrorMessage = "La categoría es requerida")]
    public int CategoriaId { get; set; }

    public bool Activo { get; set; } = true;
    public bool Disponible { get; set; } = true;
}

public class ProductoIngredienteViewModel
{
    public int ProductoId { get; set; }
    public int IngredienteId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }
    public string NombreIngrediente { get; set; } = string.Empty;
}
