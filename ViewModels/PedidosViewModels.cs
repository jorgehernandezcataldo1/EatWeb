using EatWeb.Models.Enums;

namespace EatWeb.ViewModels;

public class PedidosIndexViewModel
{
    public List<PedidoMesaViewModel> Mesas { get; set; } = new();
}

public class PedidoMesaViewModel
{
    public int SesionId { get; set; }
    public int MesaId { get; set; }
    public int MesaNumero { get; set; }

    public bool CuentaSolicitada { get; set; }

    public List<PedidoResumenViewModel> Pedidos { get; set; } = new();
}

public class PedidoResumenViewModel
{
    public int Id { get; set; }

    public string ComensalNombre { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public DateTime FechaCreacion { get; set; }

    public List<DetallePedidoResumenViewModel> Detalles { get; set; } = new();
}

public class DetallePedidoResumenViewModel
{
    public string Producto { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public decimal Subtotal { get; set; }

    public string? Observacion { get; set; }

    public List<string> Personalizaciones { get; set; } = new();
}

public class CuentaViewModel
{
    public int SesionId { get; set; }

    public int MesaNumero { get; set; }

    public decimal Total { get; set; }

    public decimal TotalVerificado { get; set; }

    public decimal MontoIgualBase { get; set; }

    public decimal MontoIgualUltimaPersona { get; set; }

    public List<CuentaPersonaViewModel> Personas { get; set; } = new();
}

public class CuentaPersonaViewModel
{
    public int ComensalId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal TotalConsumido { get; set; }

    public List<string> Items { get; set; } = new();
}
