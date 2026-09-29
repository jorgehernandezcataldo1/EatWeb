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
    public DateTime FechaApertura { get; set; }
    public bool CuentaSolicitada { get; set; }
    public DateTime? CuentaSolicitadaEn { get; set; }

    public decimal Total { get; set; }
    public decimal TotalVerificado { get; set; }

    public List<CuentaPersonaViewModel> Personas { get; set; } = new();
    public List<CuentaItemViewModel> Items { get; set; } = new();
}

public class CuentaPersonaViewModel
{
    public int ComensalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }
    public decimal TotalConsumido { get; set; }
    public List<CuentaItemViewModel> Items { get; set; } = new();
}

public class CuentaItemViewModel
{
    public int PedidoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public string? Observacion { get; set; }
    public List<string> Personalizaciones { get; set; } = new();
    public string EstadoPedido { get; set; } = string.Empty;
}

public class DivisionCuentaViewModel
{
    public int SesionId { get; set; }
    public int MesaNumero { get; set; }
    public decimal Total { get; set; }
    public string Modo { get; set; } = "Igual";  // Igual | PorConsumo | Personalizado
    public int CantidadPartes { get; set; }
    public List<CuentaPersonaViewModel> Personas { get; set; } = new();

    // Para "Personalizado": grupo → lista de comensales
    public List<GrupoDivisionViewModel> Grupos { get; set; } = new();
}

public class GrupoDivisionViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public List<int> ComensalIds { get; set; } = new();

    // Calculado
    public decimal Total { get; set; }
    public decimal MontoPorPersona { get; set; }
    public int CantidadPersonas { get; set; }
}

// Para procesar el POST
public class CerrarConDivisionInputViewModel
{
    public int SesionId { get; set; }
    public string Modo { get; set; } = string.Empty;
    public List<GrupoDivisionInput> Grupos { get; set; } = new();
}

public class GrupoDivisionInput
{
    public string Nombre { get; set; } = string.Empty;
    public List<int> ComensalIds { get; set; } = new();
}
