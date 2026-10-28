using System.Data;
using System.Security.Cryptography;
using System.Text;
using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    private readonly OperacionRealtimeService _realtime;

    public PedidoService(
        ApplicationDbContext context,
        ILogger<PedidoService> logger,
        OperacionRealtimeService realtime)
    {
        _context = context;
        _logger = logger;
        _realtime = realtime;
    }

    public async Task<CarritoCalculado> CalcularCarritoAsync(
        CarritoSesion carrito,
        int? restauranteId = null)
    {
        var resultado = new CarritoCalculado();

        if (carrito.Lineas.Count == 0)
        {
            resultado.Errores.Add("Carrito vacío");
            return resultado;
        }

        var productoIds = carrito.Lineas
            .Select(l => l.ProductoId)
            .Distinct()
            .ToList();

        // Dos lecturas en lote en vez de dos consultas por cada línea del carrito.
        var productosQuery = _context.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
                .ThenInclude(c => c!.Estacion)
            .Where(p => productoIds.Contains(p.Id));

        if (restauranteId.HasValue)
            productosQuery = productosQuery.Where(p => p.RestauranteId == restauranteId.Value);

        var productos = await productosQuery
            .ToDictionaryAsync(p => p.Id);

        var ingredientesPorProducto = (await _context.ProductoIngredientes
                .AsNoTracking()
                .Include(pi => pi.Ingrediente)
                .Where(pi => productoIds.Contains(pi.ProductoId))
                .ToListAsync())
            .GroupBy(pi => pi.ProductoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var linea in carrito.Lineas)
        {
            if (!productos.TryGetValue(linea.ProductoId, out var producto))
            {
                resultado.Errores.Add($"Producto {linea.ProductoId} no encontrado");
                continue;
            }

            if (!producto.Activo || !producto.Disponible)
            {
                resultado.Errores.Add($"{producto.Nombre} no está disponible");
                continue;
            }

            if (producto.Categoria == null || !producto.Categoria.Activa)
            {
                resultado.Errores.Add($"{producto.Nombre} pertenece a una categoría no disponible");
                continue;
            }

            if (producto.Categoria.Estacion == null || !producto.Categoria.Estacion.Activa)
            {
                resultado.Errores.Add($"{producto.Nombre} no tiene una estación de preparación activa");
                continue;
            }

            ingredientesPorProducto.TryGetValue(
                linea.ProductoId,
                out var ingredientesProducto);

            ingredientesProducto ??= new List<ProductoIngrediente>();

            var personalizaciones = new List<PersonalizacionLegible>();
            var montoPrecioExtra = 0m;

            foreach (var idQuitar in linea.IngredientesQuitar.Distinct())
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

            foreach (var idAgregar in linea.IngredientesAgregar.Distinct())
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
        string? observacionGeneral,
        Guid requestId)
    {
        var resultado = new ResultadoPedido();

        if (requestId == Guid.Empty)
        {
            resultado.Errores.Add("Identificador de envío inválido. Recarga el carrito.");
            return resultado;
        }

        var observacion = (observacionGeneral ?? string.Empty).Trim();
        if (observacion.Length > 300)
        {
            resultado.Errores.Add("La nota general no puede superar 300 caracteres.");
            return resultado;
        }

        // Si el primer POST alcanzó a persistir y la respuesta se perdió,
        // un reintento con la misma clave devuelve el pedido ya creado.
        var existente = await _context.Pedidos
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.ComensalId == comensalId &&
                p.RequestId == requestId);

        if (existente != null)
            return ResolverReintento(existente, carrito, observacion);

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

        var fingerprint = CalcularFingerprint(carrito, observacion);

        await using var transaccion = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        Pedido? pedido = null;

        try
        {
            // Segunda lectura dentro de la transacción para cubrir reintentos que
            // entraron entre la comprobación inicial y el BeginTransaction.
            existente = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ComensalId == comensalId &&
                    p.RequestId == requestId);

            if (existente != null)
            {
                await transaccion.RollbackAsync();
                return ResolverReintento(existente, carrito, observacion, fingerprint);
            }

            var comensal = await _context.Comensales
                .Include(c => c.MesaSesion)
                    .ThenInclude(s => s!.Mesa)
                .FirstOrDefaultAsync(c => c.Id == comensalId);

            if (comensal == null)
            {
                await transaccion.RollbackAsync();
                resultado.Errores.Add("Comensal no encontrado");
                return resultado;
            }

            var sesion = comensal.MesaSesion;
            if (sesion == null || sesion.FechaCierre.HasValue)
            {
                await transaccion.RollbackAsync();
                resultado.Errores.Add("Sesión cerrada o no disponible");
                return resultado;
            }

            if (sesion.Mesa == null || !sesion.Mesa.Activa)
            {
                await transaccion.RollbackAsync();
                resultado.Errores.Add("Mesa no disponible");
                return resultado;
            }

            if (sesion.CuentaSolicitadaEn.HasValue)
            {
                await transaccion.RollbackAsync();
                resultado.Errores.Add("La cuenta ya ha sido solicitada");
                return resultado;
            }

            var carritoCalculado = await CalcularCarritoAsync(
                carrito,
                sesion.Mesa.RestauranteId);

            if (carritoCalculado.Errores.Count > 0)
            {
                await transaccion.RollbackAsync();
                resultado.Errores = carritoCalculado.Errores;
                return resultado;
            }

            pedido = new Pedido
            {
                MesaSesionId = sesion.Id,
                ComensalId = comensalId,
                RequestId = requestId,
                RequestFingerprint = fingerprint,
                Estado = EstadoPedido.Pendiente,
                Total = carritoCalculado.Total,
                ObservacionGeneral = observacion,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Pedidos.Add(pedido);

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

                detalle.Historial.Add(new HistorialEstadoDetallePedido
                {
                    EstadoAnterior = null,
                    EstadoNuevo = EstadoDetallePedido.Pendiente,
                    Fecha = DateTime.UtcNow,
                    UsuarioId = null,
                    Motivo = null
                });

                pedido.Detalles.Add(detalle);
            }

            pedido.Historial.Add(new HistorialEstadoPedido
            {
                EstadoAnterior = null,
                EstadoNuevo = EstadoPedido.Pendiente,
                Fecha = DateTime.UtcNow,
                UsuarioId = null
            });

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch (Exception ex) when (
            ex is DbUpdateException ||
            ex is PostgresException pg && pg.SqlState is "40001" or "40P01" or "23505")
        {
            // PostgreSQL puede rechazar transacciones SERIALIZABLE por conflicto
            // (40001), deadlock (40P01) o clave duplicada (23505).
            // Releer tras rollback permite recuperar un pedido ya confirmado.
            await transaccion.RollbackAsync();
            _context.ChangeTracker.Clear();

            // La carrera esperable es que otro POST con el mismo RequestId
            // haya ganado el índice único. Si existe y el payload coincide,
            // el resultado es idempotente y no un error.
            existente = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ComensalId == comensalId &&
                    p.RequestId == requestId);

            if (existente != null)
                return ResolverReintento(existente, carrito, observacion, fingerprint);

            _logger.LogError(ex,
                "Error persistiendo pedido idempotente para comensal {ComensalId}, request {RequestId}",
                comensalId,
                requestId);

            resultado.Errores.Add(
                "No pudimos guardar el pedido. Intenta nuevamente o avisa al personal.");
            return resultado;
        }

        resultado.Ok = true;
        resultado.PedidoId = pedido!.Id;

        // SignalR ocurre después del commit: es una notificación, no la fuente de verdad.
        try
        {
            await NotificarPedidoAsync(pedido.Id, "PedidoCreado");
        }
        catch (Exception ex)
        {
            // El pedido ya fue confirmado en PostgreSQL: no informar un fallo
            // ni inducir al cliente a reenviar por una caída de SignalR.
            _logger.LogError(ex,
                "Pedido {PedidoId} confirmado, pero falló la notificación realtime",
                pedido.Id);
        }

        return resultado;
    }

    private ResultadoPedido ResolverReintento(
        Pedido existente,
        CarritoSesion carrito,
        string observacion,
        string? fingerprintActual = null)
    {
        // Tras un envío exitoso el controller vacía el carrito. En ese caso la
        // misma RequestId puede recuperarse sin exigir reconstruir el payload.
        if (carrito.Lineas.Count > 0)
        {
            fingerprintActual ??= CalcularFingerprint(carrito, observacion);

            if (!string.Equals(
                    existente.RequestFingerprint,
                    fingerprintActual,
                    StringComparison.Ordinal))
            {
                return new ResultadoPedido
                {
                    Errores =
                    {
                        "Este identificador de envío ya fue usado con un pedido distinto. Recarga el carrito."
                    }
                };
            }
        }

        return new ResultadoPedido
        {
            Ok = true,
            PedidoId = existente.Id
        };
    }

    private static string CalcularFingerprint(
        CarritoSesion carrito,
        string observacionGeneral)
    {
        var builder = new StringBuilder();
        builder.Append("obs:")
            .Append(observacionGeneral.Length)
            .Append(':')
            .Append(observacionGeneral)
            .Append('|');

        foreach (var linea in carrito.Lineas.OrderBy(l => l.LineaId))
        {
            builder.Append(linea.LineaId).Append(';')
                .Append(linea.ProductoId).Append(';')
                .Append(linea.Cantidad).Append(';')
                .Append(linea.Observacion?.Length ?? 0).Append(':')
                .Append(linea.Observacion ?? string.Empty).Append(';');

            builder.Append("q[");
            foreach (var id in linea.IngredientesQuitar.Distinct().OrderBy(x => x))
                builder.Append(id).Append(',');

            builder.Append("];a[");
            foreach (var id in linea.IngredientesAgregar.Distinct().OrderBy(x => x))
                builder.Append(id).Append(',');

            builder.Append("]|");
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
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
            AplicarEstadoDetalle(detalle, nuevoEstado, usuarioId);

        await RecalcularPedidoAsync(pedido, usuarioId);
        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        await NotificarPedidoAsync(pedido.Id, "PedidoActualizado");

        return new ResultadoPedido { Ok = true, PedidoId = pedido.Id };
    }

    public async Task<ResultadoPedido> CambiarEstadoDetalleAsync(
        int detalleId,
        string nuevoEstado,
        string usuarioId,
        string? motivo = null)
    {
        var detalle = await _context.DetallesPedidos
            .Include(d => d.Pedido)
                .ThenInclude(p => p!.Detalles)
            .FirstOrDefaultAsync(d => d.Id == detalleId);

        if (detalle?.Pedido == null)
            return new ResultadoPedido { Errores = { "Detalle no encontrado" } };

        if (!EsTransicionValida(detalle.Estado, nuevoEstado))
            return new ResultadoPedido { Errores = { $"Transición de {detalle.Estado} a {nuevoEstado} no permitida" } };

        var motivoNormalizado = string.IsNullOrWhiteSpace(motivo)
            ? null
            : motivo.Trim();

        if (nuevoEstado == EstadoDetallePedido.Cancelado)
        {
            if (motivoNormalizado == null || motivoNormalizado.Length < 3 || motivoNormalizado.Length > 200)
            {
                return new ResultadoPedido
                {
                    Errores = { "La anulación requiere un motivo entre 3 y 200 caracteres." }
                };
            }

            var tienePagoConfirmado = await _context.PagoDetalles
                .AsNoTracking()
                .AnyAsync(pd =>
                    pd.DetallePedidoId == detalleId &&
                    pd.Pago!.Estado == EstadoPago.Confirmado);

            if (tienePagoConfirmado)
            {
                return new ResultadoPedido
                {
                    Errores = { "No se puede anular un consumo que ya tiene un pago confirmado." }
                };
            }
        }

        AplicarEstadoDetalle(detalle, nuevoEstado, usuarioId, motivoNormalizado);
        var resultado = await RecalcularYGuardarPedidoAsync(detalle.Pedido, usuarioId);

        if (resultado.Ok)
            await NotificarPedidoAsync(detalle.PedidoId, "PedidoActualizado");

        return resultado;
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
                AplicarEstadoDetalle(detalle, nuevoEstado, usuarioId);
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

        var pedidoReferencia = pedidos.FirstOrDefault(p => p.Detalles.Any(d =>
            d.Estado == nuevoEstado ||
            d.Estado == EstadoDetallePedido.Entregado));

        if (pedidoReferencia != null)
            await NotificarPedidoAsync(pedidoReferencia.Id, "PedidoActualizado");

        return new ResultadoPedido { Ok = true };
    }

    private async Task NotificarPedidoAsync(int pedidoId, string tipo)
    {
        var datos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.Id == pedidoId)
            .Select(p => new
            {
                p.Id,
                SesionId = p.MesaSesionId,
                RestauranteId = p.MesaSesion!.Mesa!.RestauranteId,
                GarzonId = p.MesaSesion.GarzonId ?? p.MesaSesion.Mesa.GarzonId,
                Estaciones = p.Detalles
                    .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                    .Select(d => d.EstacionNombre)
                    .Distinct()
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (datos == null)
            return;

        await _realtime.PublicarAsync(
            datos.RestauranteId,
            tipo,
            datos.SesionId,
            datos.GarzonId,
            datos.Estaciones);
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

    private static void AplicarEstadoDetalle(
        DetallePedido detalle,
        string nuevoEstado,
        string? usuarioId,
        string? motivo = null)
    {
        var anterior = detalle.Estado;
        if (anterior == nuevoEstado)
            return;

        detalle.Estado = nuevoEstado;
        var ahora = DateTime.UtcNow;

        if (nuevoEstado == EstadoDetallePedido.EnPreparacion)
            detalle.FechaInicioPreparacion ??= ahora;

        if (nuevoEstado == EstadoDetallePedido.Listo)
            detalle.FechaListo ??= ahora;

        if (nuevoEstado == EstadoDetallePedido.Entregado)
            detalle.FechaEntregado ??= ahora;

        detalle.Historial.Add(new HistorialEstadoDetallePedido
        {
            EstadoAnterior = anterior,
            EstadoNuevo = nuevoEstado,
            Fecha = ahora,
            UsuarioId = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId,
            Motivo = motivo
        });
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