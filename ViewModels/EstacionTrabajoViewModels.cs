namespace EatWeb.ViewModels;

public class EstacionTrabajoViewModel
{
    public string Estacion { get; set; } = string.Empty;
    public List<EstacionTrabajoItemViewModel> Items { get; set; } = new();
    public List<EstacionTrabajoPedidoViewModel> Pedidos { get; set; } = new();
}

public class EstacionTrabajoPedidoViewModel
{
    public int MesaNumero { get; set; }
    public int PedidoId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaPedido { get; set; }
    public int MinutosEspera { get; set; }
    public bool Urgente { get; set; }
    public List<EstacionTrabajoItemViewModel> Items { get; set; } = new();
}

public class EstacionTrabajoItemViewModel
{
    public int DetalleId { get; set; }
    public int MesaNumero { get; set; }
    public int PedidoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public DateTime FechaPedido { get; set; }
    public List<string> Personalizaciones { get; set; } = new();
}
