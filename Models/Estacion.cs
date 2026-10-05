namespace EatWeb.Models;

public class Estacion
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }

    public Restaurante? Restaurante { get; set; }
    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
}
