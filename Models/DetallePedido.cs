namespace EatWeb.Models;

public class DetallePedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
    public string? Observacion { get; set; }
    public decimal Subtotal { get; set; }

    // Relaciones
    public Pedido? Pedido { get; set; }
    public Producto? Producto { get; set; }
    public ICollection<DetallePedidoIngrediente> Ingredientes { get; set; } = new List<DetallePedidoIngrediente>();
}
