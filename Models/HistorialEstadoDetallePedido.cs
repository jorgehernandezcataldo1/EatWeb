namespace EatWeb.Models;

public class HistorialEstadoDetallePedido
{
    public int Id { get; set; }
    public int DetallePedidoId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? UsuarioId { get; set; }
    public string? Motivo { get; set; }

    public DetallePedido? DetallePedido { get; set; }
    public ApplicationUser? Usuario { get; set; }
}
