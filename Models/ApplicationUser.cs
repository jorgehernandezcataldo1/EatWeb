using Microsoft.AspNetCore.Identity;

namespace EatWeb.Models;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public ICollection<CadenaMiembro> Cadenas { get; set; }
        = new List<CadenaMiembro>();

    public ICollection<RestauranteMiembro> Restaurantes { get; set; }
        = new List<RestauranteMiembro>();

    public ICollection<Mesa> Mesas { get; set; }
        = new List<Mesa>();

    public ICollection<MesaSesion> SesionesAtendidas { get; set; }
        = new List<MesaSesion>();

    public ICollection<HistorialEstadoPedido> HistorialesEstadoPedido { get; set; }
        = new List<HistorialEstadoPedido>();
}
