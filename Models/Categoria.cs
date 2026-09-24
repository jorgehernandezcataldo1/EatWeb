namespace EatWeb.Models;

public class Categoria
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activa { get; set; } = true;

    // Relaciones
    public Restaurante? Restaurante { get; set; }
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
