using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class RestauranteMiembro
{
    public int RestauranteId { get; set; }
    public Restaurante Restaurante { get; set; } = null!;

    public string UsuarioId { get; set; } = null!;
    public ApplicationUser Usuario { get; set; } = null!;

    public RolRestaurante Rol { get; set; }

    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
}
