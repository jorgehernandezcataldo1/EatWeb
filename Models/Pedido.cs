using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class Pedido
{
    public int Id { get; set; }
    public int MesaSesionId { get; set; }
    public int ComensalId { get; set; }
    public string Estado { get; set; } = EstadoPedido.Pendiente;
    public decimal Total { get; set; }
    public string? ObservacionGeneral { get; set; }
    public DateTime FechaCreacion { get; set; }

    // Relaciones
    public MesaSesion? MesaSesion { get; set; }
    public Comensal? Comensal { get; set; }
    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public ICollection<HistorialEstadoPedido> Historial { get; set; } = new List<HistorialEstadoPedido>();
}
