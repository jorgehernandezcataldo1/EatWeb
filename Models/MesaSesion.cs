using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class MesaSesion
{
    public int Id { get; set; }
    public int MesaId { get; set; }
    public string Estado { get; set; } = EstadoSesion.Abierta;
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime? CuentaSolicitadaEn { get; set; }

    // Relaciones
    public Mesa? Mesa { get; set; }
    public ICollection<Comensal> Comensales { get; set; } = new List<Comensal>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public Cuenta? Cuenta { get; set; }
}
