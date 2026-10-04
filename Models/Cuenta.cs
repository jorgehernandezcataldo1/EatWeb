using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class Cuenta
{
    public int Id { get; set; }
    public int MesaSesionId { get; set; }
    public string Estado { get; set; } = EstadoCuenta.Abierta;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }

    public MesaSesion? MesaSesion { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
