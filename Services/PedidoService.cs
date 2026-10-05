using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class LineaCarritoCalculada
{
    public Guid LineaId { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public int EstacionId { get; set; }
    public string EstacionNombre { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
    public List<PersonalizacionLegible> Personalizaciones { get; set; } = new();
    public decimal Subtotal { get; set; }
    public string Observacion { get; set; } = string.Empty;
}

public class PersonalizacionLegible
{
    public int IngredienteId { get; set; }          // NUEVO
    public string TipoAccion { get; set; } = string.Empty; // "Sin" / "Agregar"
    public string NombreIngrediente { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }
}

public class CarritoCalculado
{
    public List<LineaCarritoCalculada> Lineas { get; set; } = new();
    public decimal Total { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class ResultadoPedido
{
    public bool Ok { get; set; }
    public int? PedidoId { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class PedidoService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PedidoService> _logger;

    public PedidoService(
        ApplicationDbContext context,
        ILogger<PedidoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CarritoCalculado> CalcularCarritoAsync(CarritoSesion carrito, int? restauranteId = null)
    {
        var resultado = new CarritoCalculado();

        if (carrito.Lineas.Count == 0)
        {
            resultado.Errores.Add("Carrito vacío");
            return resultado;
        }

        foreach (var linea in carrito.Lineas)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                    .ThenInclude(c => c!.Estacion)
                .FirstOrDefaultAsync(p => p.Id == linea.ProductoId && (!restauranteId.HasValue || p.RestauranteId == restauranteId.Value));

            if (producto == null)
            {
                resultado.Errores.Add($"Producto {linea.ProductoId} no encontrado");
                continue;
            }

            if (!producto.Activo || !producto.Disponible)
            {
                resultado.Errores.Add($"{producto.Nombre} no está disponible");
                continue;
            }

            if (producto.Categoria?.Estacion == null || !producto.Categoria.Estacion.Activa)
            {
                resultado.Errores.Add($"{producto.Nombre} no tiene una estación de preparación activa");
                continue;
            }

            var ingredientesProducto = await _context.ProductoIngredientes
                .AsNoTracking()
                .Include(pi => pi.Ingrediente)
                .Where(pi => pi.ProductoId == linea.ProductoId)
                .ToListAsync();

            var personalizaciones = new List<PersonalizacionLegible>();
            var montoPrecioExtra = 0m;

            // QUITAR: solo válidos si son "Incluido"
            foreach (var idQuitar in linea.IngredientesQuitar)
            {
                var pi = ingredientesProducto.FirstOrDefault(x => x.IngredienteId == idQuitar);

                if (pi == null || pi.Tipo != TipoIngrediente.Incluido)
                {
                    resultado.Errores.Add(
                        $"No se puede quitar el ingrediente {idQuitar} de {producto.Nombre}");
                    continue;
                }

                if (pi.Ingrediente == null || !pi.Ingrediente.Activo)
                {
                    resultado.Errores.Add("Ingrediente no disponible");
                    continue;
                }

                personalizaciones.Add(new PersonalizacionLegible
                {
                    IngredienteId = pi.IngredienteId,
                    TipoAccion = "Sin",
                    NombreIngrediente = pi.Ingrediente.Nombre,
                    PrecioExtra = 0
                });
            }

            // AGREGAR: solo válidos si son "Extra"
            foreach (var idAgregar in linea.IngredientesAgregar)
            {
                var pi = ingredientesProducto.FirstOrDefault(x => x.IngredienteId == idAgregar);

                if (pi == null || pi.Tipo != TipoIngrediente.Extra)
                {
                    resultado.Errores.Add(
                        $"No se puede agregar el ingrediente {idAgregar} a {producto.Nombre}");
                    continue;
                }

                if (pi.Ingrediente == null || !pi.Ingrediente.Activo)
                {
                    resultado.Errores.Add("Ingrediente no disponible");
                    continue;
                }

                montoPrecioExtra += pi.PrecioExtra;

                personalizaciones.Add(new PersonalizacionLegible
                {
                    IngredienteId = pi.IngredienteId,
                    TipoAccion = "Agregar",
                    NombreIngrediente = pi.Ingrediente.Nombre,
                    PrecioExtra = pi.PrecioExtra
                });
            }

            var precioUnitarioConExtras = producto.Precio + montoPrecioExtra;
            var subtotal = precioUnitarioConExtras * linea.Cantidad;

            resultado.Lineas.Add(new LineaCarritoCalculada
            {
                LineaId = linea.LineaId,
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                EstacionId = producto.Categoria.Estacion.Id,
                EstacionNombre = producto.Categoria.Estacion.Nombre,
                PrecioUnitario = producto.Precio,
                Cantidad = linea.Cantidad,
                Personalizaciones = personalizaciones,
                Subtotal = subtotal,
                Observacion = linea.Observacion
            });

            resultado.Total += subtotal;
        }

        return resultado;
    }

    public async Task<ResultadoPedido> CrearPedidoAsync(
        int comensalId,
        CarritoSesion carrito,
        string? observacionGeneral)
    {
        var resultado = new ResultadoPedido();

        var comensal = await _context.Comensales
            .Include(c => c.MesaSesion)
                .ThenInclude(s => s!.Mesa)
            .FirstOrDefaultAsync(c => c.Id == comensalId);

        if (comensal == null)
        {
            resultado.Errores.Add("Comensal no encontrado");
            return resultado;
        }

        var sesion = comensal.MesaSesion;
        if (sesion == null || sesion.FechaCierre.HasValue)
        {
            resultado.Errores.Add("Sesión cerrada o no disponible");
            return resultado;
        }

        if (sesion.Mesa == null || !sesion.Mesa.Activa)
        {
            resultado.Errores.Add("Mesa no disponible");
            return resultado;
        }

        if (sesion.CuentaSolicitadaEn.HasValue)
        {
            resultado.Errores.Add("La cuenta ya ha sido solicitada");
            return resultado;
        }

        if (carrito.Lineas.Count == 0)
        {
            resultado.Errores.Add("Carrito vacío");
            return resultado;
        }

        foreach (var linea in carrito.Lineas)
        {
            if (linea.Cantidad < 1 || linea.Cantidad > 20)
            {
                resultado.Errores.Add($"Cantidad inválida: {linea.Cantidad}");
                return resultado;
            }
        }

        var carritoCalculado = await CalcularCarritoAsync(carrito, sesion.Mesa.RestauranteId);
        if (carritoCalculado.Errores.Count > 0)
        {
            resultado.Errores = carritoCalculado.Errores;
            return resultado;
        }

        // === Crear pedido ===
        var pedido = new Pedido
        {
            MesaSesionId = sesion.Id,
            ComensalId = comensalId,
            Estado = EstadoPedido.Pendiente,
            Total = carritoCalculado.Total,
            ObservacionGeneral = observacionGeneral ?? string.Empty,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Pedidos.Add(pedido);

        // === Crear detalles ===
        foreach (var lineaCalculada in carritoCalculado.Lineas)
        {
            var detalle = new DetallePedido
            {
                ProductoId = lineaCalculada.ProductoId,
                NombreProducto = lineaCalculada.NombreProducto,
                PrecioUnitario = lineaCalculada.PrecioUnitario,
                Cantidad = lineaCalculada.Cantidad,
                Observacion = lineaCalculada.Observacion,
                Subtotal = lineaCalculada.Subtotal,
                EstacionId = lineaCalculada.EstacionId,
                EstacionNombre = lineaCalculada.EstacionNombre,
                Estado = EstadoDetallePedido.Pendiente
            };

            foreach (var pers in lineaCalculada.Personalizaciones)
            {
                detalle.Ingredientes.Add(new DetallePedidoIngrediente
                {
                    IngredienteId = pers.IngredienteId,
                    NombreIngrediente = pers.NombreIngrediente,
                    Accion = pers.TipoAccion == "Sin"
                        ? AccionIngrediente.Quitar
                        : AccionIngrediente.Agregar,
                    PrecioExtra = pers.PrecioExtra
                });
            }

            pedido.Detalles.Add(detalle);
        }

        // === Historial ===
        pedido.Historial.Add(new HistorialEstadoPedido
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadoPedido.Pendiente,
            Fecha = DateTime.UtcNow,
            UsuarioId = null
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Error persistiendo pedido para comensal {ComensalId} en sesión {SesionId}",
                comensalId,
                sesion.Id);

            resultado.Errores.Add(
                "No pudimos guardar el pedido. Intenta nuevamente o avisa al personal.");
            return resultado;
        }

        resultado.Ok = true;
        resultado.PedidoId = pedido.Id;
        return resultado;
    }

    public async Task<ResultadoPedido> CambiarEstadoAsync(
        int pedidoId,
        string nuevoEstado,
        string usuarioId)
    {
        await using var transaccion = await _context.Database.BeginTransactionAsync();

        var pedido = await _context.Pedidos
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == pedidoId);

        if (pedido == null)
            return new ResultadoPedido { Errores = { "Pedido no encontrado" } };

        var detallesElegibles = pedido.Detalles
            .Where(d => EsTransicionValida(d.Estado, nuevoEstado))
            .ToList();

        if (detallesElegibles.Count == 0)
        {
            await transaccion.RollbackAsync();
            return new ResultadoPedido
            {
                Errores = { $"No hay ítems del pedido que puedan pasar a {nuevoEstado}" }
            };
        }

        foreach (var detalle in detallesElegibles)
            AplicarEstadoDetalle(detalle, nuevoEstado);

        await RecalcularPedidoAsync(pedido, usuarioId);
        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        return new ResultadoPedido { Ok = true, PedidoId = pedido.Id };
    }

    public async Task<ResultadoPedido> CambiarEstadoDetalleAsync(
        int detalleId,
        string nuevoEstado,
        string usuarioId)
    {
        var detalle = await _context.DetallesPedidos
            .Include(d => d.Pedido)
                .ThenInclude(p => p!.Detalles)
            .FirstOrDefaultAsync(d => d.Id == detalleId);

        if (detalle?.Pedido == null)
            return new ResultadoPedido { Errores = { "Detalle no encontrado" } };

        if (!EsTransicionValida(detalle.Estado, nuevoEstado))
            return new ResultadoPedido { Errores = { $"Transición de {detalle.Estado} a {nuevoEstado} no permitida" } };

        AplicarEstadoDetalle(detalle, nuevoEstado);
        return await RecalcularYGuardarPedidoAsync(detalle.Pedido, usuarioId);
    }

    public async Task<ResultadoPedido> CambiarEstadoMesaAsync(
        int sesionId,
        string nuevoEstado,
        string usuarioId,
        int? estacionId = null)
    {
        if (nuevoEstado != EstadoDetallePedido.EnPreparacion &&
            nuevoEstado != EstadoDetallePedido.Listo &&
            nuevoEstado != EstadoDetallePedido.Entregado)
        {
            return new ResultadoPedido
            {
                Errores = { "El estado masivo debe ser EnPreparacion, Listo o Entregado" }
            };
        }

        await using var transaccion = await _context.Database.BeginTransactionAsync();

        var pedidos = await _context.Pedidos
            .Include(p => p.Detalles)
            .Where(p => p.MesaSesionId == sesionId && p.Estado != EstadoPedido.Cancelado)
            .ToListAsync();

        var cambios = 0;

        foreach (var pedido in pedidos)
        {
            var detallesElegibles = pedido.Detalles
                .Where(d =>
                    (!estacionId.HasValue || d.EstacionId == estacionId.Value) &&
                    EsTransicionValida(d.Estado, nuevoEstado))
                .ToList();

            foreach (var detalle in detallesElegibles)
            {
                AplicarEstadoDetalle(detalle, nuevoEstado);
                cambios++;
            }

            if (detallesElegibles.Count > 0)
                await RecalcularPedidoAsync(pedido, usuarioId);
        }

        if (cambios == 0)
        {
            await transaccion.RollbackAsync();
            return new ResultadoPedido
            {
                Errores =
                {
                    estacionId.HasValue
                        ? $"No hay ítems elegibles de la estación seleccionada para pasar a {nuevoEstado}"
                        : $"No hay ítems elegibles en la mesa para pasar a {nuevoEstado}"
                }
            };
        }

        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        return new ResultadoPedido { Ok = true };
    }

    private async Task<ResultadoPedido> RecalcularYGuardarPedidoAsync(Pedido pedido, string usuarioId)
    {
        await RecalcularPedidoAsync(pedido, usuarioId);
        await _context.SaveChangesAsync();
        return new ResultadoPedido { Ok = true, PedidoId = pedido.Id };
    }

    private Task RecalcularPedidoAsync(Pedido pedido, string usuarioId)
    {
        // El total operacional excluye ítems cancelados. El snapshot de Subtotal
        // se mantiene en el detalle para auditoría, pero no se cobra.
        pedido.Total = pedido.Detalles
            .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
            .Sum(d => d.Subtotal);

        var anterior = pedido.Estado;
        var nuevo = CalcularEstadoPedido(pedido.Detalles);

        if (anterior != nuevo)
        {
            pedido.Estado = nuevo;
            _context.HistorialesEstadoPedido.Add(new HistorialEstadoPedido
            {
                PedidoId = pedido.Id,
                EstadoAnterior = anterior,
                EstadoNuevo = nuevo,
                Fecha = DateTime.UtcNow,
                UsuarioId = usuarioId
            });
        }

        return Task.CompletedTask;
    }

    public static string CalcularEstadoPedido(IEnumerable<DetallePedido> detalles)
    {
        var estados = detalles.Select(d => d.Estado).ToList();
        if (estados.Count == 0) return EstadoPedido.Pendiente;
        if (estados.All(e => e == EstadoDetallePedido.Cancelado)) return EstadoPedido.Cancelado;
        if (estados.Where(e => e != EstadoDetallePedido.Cancelado).All(e => e == EstadoDetallePedido.Entregado)) return EstadoPedido.Entregado;
        if (estados.Where(e => e != EstadoDetallePedido.Cancelado).All(e => e == EstadoDetallePedido.Listo || e == EstadoDetallePedido.Entregado)) return EstadoPedido.Listo;
        if (estados.Any(e => e == EstadoDetallePedido.EnPreparacion || e == EstadoDetallePedido.Listo || e == EstadoDetallePedido.Entregado)) return EstadoPedido.EnPreparacion;
        return EstadoPedido.Pendiente;
    }

    private static void AplicarEstadoDetalle(DetallePedido detalle, string nuevoEstado)
    {
        detalle.Estado = nuevoEstado;
        var ahora = DateTime.UtcNow;
        if (nuevoEstado == EstadoDetallePedido.EnPreparacion) detalle.FechaInicioPreparacion ??= ahora;
        if (nuevoEstado == EstadoDetallePedido.Listo) detalle.FechaListo ??= ahora;
        if (nuevoEstado == EstadoDetallePedido.Entregado) detalle.FechaEntregado ??= ahora;
    }

    private static bool EsTransicionValida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            var x when x == EstadoDetallePedido.Pendiente =>
                nuevoEstado == EstadoDetallePedido.EnPreparacion ||
                nuevoEstado == EstadoDetallePedido.Cancelado,

            var x when x == EstadoDetallePedido.EnPreparacion =>
                nuevoEstado == EstadoDetallePedido.Listo ||
                nuevoEstado == EstadoDetallePedido.Cancelado,

            var x when x == EstadoDetallePedido.Listo =>
                nuevoEstado == EstadoDetallePedido.Entregado,

            _ => false
        };
    }
}