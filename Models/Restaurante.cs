namespace EatWeb.Models;

public class Restaurante
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool Activo { get; set; } = true;

    // Relaciones
    public ICollection<ApplicationUser> Usuarios { get; set; } = new List<ApplicationUser>();
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public ICollection<Ingrediente> Ingredientes { get; set; } = new List<Ingrediente>();
}
