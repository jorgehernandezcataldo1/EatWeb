using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class CadenaMiembro
{
    public int CadenaId { get; set; }
    public Cadena Cadena { get; set; } = null!;

    public string UsuarioId { get; set; } = null!;
    public ApplicationUser Usuario { get; set; } = null!;

    public RolCadena Rol { get; set; }

    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
}
