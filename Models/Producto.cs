namespace EatWeb.Models;

public class Producto
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; } = true;
    public bool Disponible { get; set; } = true;
    public DateTime FechaCreacion { get; set; }

    // Relaciones
    public Restaurante? Restaurante { get; set; }
    public Categoria? Categoria { get; set; }
    public ICollection<ProductoIngrediente> Ingredientes { get; set; } = new List<ProductoIngrediente>();
    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}
