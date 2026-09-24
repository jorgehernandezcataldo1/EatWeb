using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class DetallePedidoIngrediente
{
    public int Id { get; set; }
    public int DetallePedidoId { get; set; }
    public int IngredienteId { get; set; }
    public string NombreIngrediente { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }

    // Relaciones
    public DetallePedido? DetallePedido { get; set; }
    public Ingrediente? Ingrediente { get; set; }
}
