using EatWeb.Services;

namespace EatWeb.ViewModels;

// ============ Ingreso ============
public class IngresoViewModel
{
    public int MesaNumero { get; set; }
    public string RestauranteNombre { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class AvisoClienteViewModel
{
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

// ============ Carta ============
public class CartaViewModel
{
    public List<CartaCategoriaViewModel> Categorias { get; set; } = new();
}

public class CartaCategoriaViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<CartaProductoViewModel> Productos { get; set; } = new();
}

public class CartaProductoViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenKey { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Disponible { get; set; }
}

// ============ Detalle de producto ============
public class ProductoDetalleViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenKey { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Disponible { get; set; }
    public List<IngredienteOpcionViewModel> Incluidos { get; set; } = new();
    public List<IngredienteOpcionViewModel> Extras { get; set; } = new();
}

public class IngredienteOpcionViewModel
{
    public int IngredienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }
}

public class AgregarCarritoInputViewModel
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public List<int> IngredientesQuitar { get; set; } = new();
    public List<int> IngredientesAgregar { get; set; } = new();
    public string? Observacion { get; set; }
}

// ============ Carrito ============
public class CarritoViewModel
{
    public List<LineaCarritoCalculada> Lineas { get; set; } = new();
    public decimal Total { get; set; }
    public List<string> Errores { get; set; } = new();
}

// ============ Mi Mesa ============
public class MiMesaViewModel
{
    public int MesaNumero { get; set; }
    public string ComensalNombre { get; set; } = string.Empty;
    public bool CuentaSolicitada { get; set; }
    public decimal TotalConsumido { get; set; }
    public List<MiPedidoViewModel> Pedidos { get; set; } = new();
    public List<SolicitudMesaResumenViewModel> Solicitudes { get; set; } = new();
}

public class MiPedidoViewModel
{
    public int Id { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime Fecha { get; set; }
    public List<string> Items { get; set; } = new();
    public List<string> Personalizaciones { get; set; } = new();
    public string? Observacion { get; set; }
}