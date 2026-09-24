using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class ProductoIngrediente
{
    public int ProductoId { get; set; }
    public int IngredienteId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }

    // Relaciones
    public Producto? Producto { get; set; }
    public Ingrediente? Ingrediente { get; set; }
}
