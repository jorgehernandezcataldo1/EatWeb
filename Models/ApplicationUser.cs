using Microsoft.AspNetCore.Identity;

namespace EatWeb.Models;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public int RestauranteId { get; set; }

    // Relaciones
    public Restaurante? Restaurante { get; set; }
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
    public ICollection<HistorialEstadoPedido> HistorialesEstadoPedido { get; set; } = new List<HistorialEstadoPedido>();
}
