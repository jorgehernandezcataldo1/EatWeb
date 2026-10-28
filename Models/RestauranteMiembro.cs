using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class RestauranteMiembro
{
    public int RestauranteId { get; set; }
    public Restaurante Restaurante { get; set; } = null!;

    public string UsuarioId { get; set; } = null!;
    public ApplicationUser Usuario { get; set; } = null!;

    public RolRestaurante Rol { get; set; }

    /// <summary>
    /// Estado local de la membresía. Desactivar un usuario en un restaurante
    /// no debe bloquear su cuenta en otros restaurantes.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
}
