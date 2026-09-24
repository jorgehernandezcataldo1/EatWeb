namespace EatWeb.Models;

public class Ingrediente
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    // Relaciones
    public Restaurante? Restaurante { get; set; }
    public ICollection<ProductoIngrediente> Productos { get; set; } = new List<ProductoIngrediente>();
    public ICollection<DetallePedidoIngrediente> DetallesIngredientes { get; set; } = new List<DetallePedidoIngrediente>();
}
