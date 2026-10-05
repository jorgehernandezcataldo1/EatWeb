namespace EatWeb.Models;

public class Restaurante
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;
    public string? LogoUrl { get; set; }

    public int? CadenaId { get; set; }
    public Cadena? Cadena { get; set; }
    public bool Activo { get; set; } = true;


    public ICollection<RestauranteMiembro> Miembros { get; set; }
        = new List<RestauranteMiembro>();

    public ICollection<Mesa> Mesas { get; set; }
        = new List<Mesa>();

    public ICollection<Categoria> Categorias { get; set; }
        = new List<Categoria>();

    public ICollection<Producto> Productos { get; set; }
        = new List<Producto>();

    public ICollection<Ingrediente> Ingredientes { get; set; }
        = new List<Ingrediente>();

    public ICollection<Estacion> Estaciones { get; set; } = new List<Estacion>();
}
