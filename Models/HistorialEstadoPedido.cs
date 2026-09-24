using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class HistorialEstadoPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? UsuarioId { get; set; }

    // Relaciones
    public Pedido? Pedido { get; set; }
    public ApplicationUser? Usuario { get; set; }
}
