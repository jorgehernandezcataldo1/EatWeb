namespace EatWeb.Models;

public class PagoDetalle
{
    public int Id { get; set; }
    public int PagoId { get; set; }
    public int DetallePedidoId { get; set; }
    public decimal MontoAsignado { get; set; }

    public Pago? Pago { get; set; }
    public DetallePedido? DetallePedido { get; set; }
}
