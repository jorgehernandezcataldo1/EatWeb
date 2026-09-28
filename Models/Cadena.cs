namespace EatWeb.Models;

public class Cadena
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;
    public string? LogoUrl { get; set; }

    public ICollection<Restaurante> Restaurantes { get; set; }
        = new List<Restaurante>();

    public ICollection<CadenaMiembro> Miembros { get; set; }
        = new List<CadenaMiembro>();
}

