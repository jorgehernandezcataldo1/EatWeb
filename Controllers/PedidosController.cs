using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
///mmfggfgfgjjj
namespace EatWeb.Controllers;

[Authorize(Roles = Roles.AdminOGarzon)]
public class PedidosController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;
    private readonly SesionService _sesionService;
    private readonly CuentaService _cuentaService;
    private readonly SolicitudMesaService _solicitudMesaService;

    public PedidosController(
        ApplicationDbContext context,
        PedidoService pedidoService,
        SesionService sesionService,
        CuentaService cuentaService,
        SolicitudMesaService solicitudMesaService)
    {
        _context = context;
        _pedidoService = pedidoService;
        _sesionService = sesionService;
        _cuentaService = cuentaService;
        _solicitudMesaService = solicitudMesaService;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await ConstruirOperacionAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Mesa(int id)
    {
        var vm = await ConstruirOperacionAsync(id);

        if (!vm.Mesas.Any())
            return NotFound();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> SolicitudesPendientes(int? sesionId = null)
    {
        Response.Headers.CacheControl = "no-store";
        var solicitudes = await ObtenerSolicitudesPendientesAsync();

        if (sesionId.HasValue)
            solicitudes = solicitudes.Where(s => s.SesionId == sesionId.Value).ToList();

        return PartialView("_SolicitudesPendientes", solicitudes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtenderSolicitud(int id)
    {
        var sesionId = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.Id == id &&
                s.MesaSesion!.Mesa!.RestauranteId == RestauranteId)
            .Select(s => (int?)s.MesaSesionId)
            .FirstOrDefaultAsync();

        var resultado = await _solicitudMesaService.AtenderAsync(
            id,
            RestauranteId,
            GetUserId() ?? string.Empty,
            User.EsAdmin());

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);
        else
            TempData["Ok"] = "Solicitud marcada como atendida.";

        return sesionId.HasValue
            ? RedirectToAction(nameof(Mesa), new { id = sesionId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
    int id,
    string nuevoEstado)
    {
        if (!EstadoPedido.Todos().Contains(nuevoEstado))
            return BadRequest();

        var pedido = await _context.Pedidos
            .Include(p => p.MesaSesion)
                .ThenInclude(s => s!.Mesa)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.MesaSesion!.Mesa!.RestauranteId == RestauranteId);

        if (pedido == null)
            return NotFound();

        if (User.EsGarzon() &&
            pedido.MesaSesion!.GarzonId != GetUserId() &&
            pedido.MesaSesion.Mesa!.GarzonId != GetUserId())
        {
            return Forbid();
        }

        var resultado = await _pedidoService.CambiarEstadoAsync(
            id,
            nuevoEstado,
            GetUserId() ?? string.Empty);

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);

        return RedirectToAction(nameof(Mesa), new { id = pedido.MesaSesionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoDetalle(int id, string nuevoEstado)
    {
        if (!EstadoDetallePedido.Todos().Contains(nuevoEstado)) return BadRequest();

        var detalle = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d => d.Id == id &&
                        d.Pedido!.MesaSesion!.Mesa!.RestauranteId == RestauranteId &&
                        (User.EsAdmin() ||
                         d.Pedido.MesaSesion.GarzonId == GetUserId() ||
                         d.Pedido.MesaSesion.Mesa.GarzonId == GetUserId()))
            .Select(d => new { d.Id, SesionId = d.Pedido!.MesaSesionId })
            .FirstOrDefaultAsync();

        if (detalle == null) return NotFound();

        var resultado = await _pedidoService.CambiarEstadoDetalleAsync(id, nuevoEstado, GetUserId() ?? string.Empty);
        if (!resultado.Ok) TempData["Error"] = string.Join(" ", resultado.Errores);
        return RedirectToAction(nameof(Mesa), new { id = detalle.SesionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoMesa(int id, string nuevoEstado, int? estacionId = null)
    {
        if (!EstadoDetallePedido.Todos().Contains(nuevoEstado)) return BadRequest();

        var permitido = await _context.MesaSesiones
            .AnyAsync(s => s.Id == id && s.FechaCierre == null &&
                           s.Mesa!.RestauranteId == RestauranteId &&
                           (User.EsAdmin() ||
                            s.GarzonId == GetUserId() ||
                            s.Mesa.GarzonId == GetUserId()));

        if (!permitido) return NotFound();

        if (estacionId.HasValue)
        {
            var estacionValida = await _context.Estaciones
                .AsNoTracking()
                .AnyAsync(e => e.Id == estacionId.Value &&
                               e.RestauranteId == RestauranteId);

            if (!estacionValida) return BadRequest();
        }

        var resultado = await _pedidoService.CambiarEstadoMesaAsync(
            id,
            nuevoEstado,
            GetUserId() ?? string.Empty,
            estacionId);
        if (!resultado.Ok) TempData["Error"] = string.Join(" ", resultado.Errores);
        return RedirectToAction(nameof(Mesa), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarMesa(int id)
    {
        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .Include(s => s.Mesa)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.GarzonId == userId ||
                 s.Mesa.GarzonId == userId));

        if (sesion == null)
            return NotFound();

        var totalCuenta = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesionId == id &&
                d.Pedido.Estado != EstadoPedido.Cancelado &&
                d.Estado != EstadoDetallePedido.Cancelado)
            .SumAsync(d => (decimal?)d.Subtotal) ?? 0;

        var totalPagado = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd =>
                pd.Pago!.Cuenta!.MesaSesionId == id &&
                pd.Pago.Estado == EstadoPago.Confirmado)
            .SumAsync(pd => (decimal?)pd.MontoAsignado) ?? 0;

        if (totalPagado < totalCuenta)
        {
            TempData["Error"] = "La mesa tiene saldo pendiente. Registra el pago antes de cerrarla.";
            return RedirectToAction(nameof(Cuenta), new { id });
        }

        var (ok, error) = await _sesionService.CerrarSesionAsync(
            id,
            forzar: false,
            usuarioId: userId);

        if (!ok)
        {
            TempData["Error"] = error ?? "No se pudo cerrar la mesa";
            return RedirectToAction(nameof(Cuenta), new { id });
        }

        TempData["Ok"] = $"Mesa {sesion.Mesa!.Numero} cerrada y liberada";
        return RedirectToAction("Index", "Mesas");
    }

    [HttpGet]
    public async Task<IActionResult> Cuenta(int id)
    {
        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.GarzonId == userId ||
                 s.Mesa.GarzonId == userId));

        if (sesion == null)
            return NotFound();

        var pagadoPorDetalle = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd =>
                pd.Pago!.Cuenta!.MesaSesionId == sesion.Id &&
                pd.Pago.Estado == EstadoPago.Confirmado)
            .GroupBy(pd => pd.DetallePedidoId)
            .Select(g => new
            {
                DetalleId = g.Key,
                Monto = g.Sum(x => x.MontoAsignado)
            })
            .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(comensal =>
            {
                var items = comensal.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .SelectMany(p => p.Detalles
                        .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                        .Select(d =>
                        {
                            var pagado = Math.Min(
                                d.Subtotal,
                                pagadoPorDetalle.GetValueOrDefault(d.Id));

                            return new CuentaItemViewModel
                            {
                                PedidoId = p.Id,
                                DetallePedidoId = d.Id,
                                ComensalId = comensal.Id,
                                ComensalNombre = comensal.Nombre,
                                Producto = d.NombreProducto,
                                Cantidad = d.Cantidad,
                                PrecioUnitario = d.PrecioUnitario,
                                Subtotal = d.Subtotal,
                                MontoPagado = pagado,
                                SaldoPendiente = Math.Max(0, d.Subtotal - pagado),
                                Observacion = d.Observacion,
                                EstadoPedido = p.Estado,
                                Personalizaciones = d.Ingredientes
                                    .Select(i =>
                                        $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} {i.NombreIngrediente}" +
                                        (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
                                    .ToList()
                            };
                        }))
                    .ToList();

                return new CuentaPersonaViewModel
                {
                    ComensalId = comensal.Id,
                    Nombre = comensal.Nombre,
                    FechaIngreso = comensal.FechaIngreso,
                    TotalConsumido = items.Sum(i => i.Subtotal),
                    TotalPagado = items.Sum(i => i.MontoPagado),
                    SaldoPendiente = items.Sum(i => i.SaldoPendiente),
                    Items = items
                };
            })
            .ToList();

        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p =>
                p.Cuenta!.MesaSesionId == sesion.Id &&
                p.Estado == EstadoPago.Confirmado)
            .OrderByDescending(p => p.FechaConfirmacion ?? p.FechaCreacion)
            .Select(p => new PagoResumenViewModel
            {
                Id = p.Id,
                Metodo = p.Metodo,
                Monto = p.Monto,
                Propina = p.Propina,
                Fecha = p.FechaConfirmacion ?? p.FechaCreacion,
                ComensalNombre = p.Comensal != null ? p.Comensal.Nombre : null
            })
            .ToListAsync();

        var itemsTodos = personas.SelectMany(p => p.Items).ToList();
        var total = itemsTodos.Sum(i => i.Subtotal);
        var totalPagado = itemsTodos.Sum(i => i.MontoPagado);
        var saldo = Math.Max(0, total - totalPagado);
        var puedeCobrar = !sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Any(p =>
                p.Estado == EstadoPedido.Pendiente ||
                p.Estado == EstadoPedido.EnPreparacion ||
                p.Estado == EstadoPedido.Listo);

        return View(new DivisionCuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            FechaApertura = sesion.FechaApertura,
            CuentaSolicitada = sesion.CuentaSolicitadaEn.HasValue,
            Total = total,
            TotalPagado = totalPagado,
            SaldoPendiente = saldo,
            PuedeCobrar = puedeCobrar,
            Personas = personas,
            Items = itemsTodos.Where(i => i.SaldoPendiente > 0).ToList(),
            Pagos = pagos,
            Modo = "Todo",
            CantidadPartes = Math.Max(1, personas.Count(p => p.SaldoPendiente > 0))
        });
    }

    [HttpGet]
    public IActionResult Dividir(int id)
    {
        return RedirectToAction(nameof(Cuenta), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarConDivision(CerrarConDivisionInputViewModel modelo)
    {
        var userId = GetUserId();

        // Para reintentos idempotentes también se permite consultar una sesión ya cerrada.
        var acceso = await _context.MesaSesiones
            .AsNoTracking()
            .Where(s =>
                s.Id == modelo.SesionId &&
                s.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.GarzonId == userId ||
                 s.Mesa.GarzonId == userId))
            .Select(s => new { s.FechaCierre })
            .FirstOrDefaultAsync();

        if (acceso == null)
            return NotFound();

        var repetido = await _cuentaService.ObtenerResultadoIdempotenteAsync(
            modelo.SesionId,
            modelo.IdempotencyKey);

        if (repetido != null)
        {
            if (!repetido.Ok)
            {
                TempData["Error"] = string.Join(" ", repetido.Errores);
                return acceso.FechaCierre.HasValue
                    ? RedirectToAction(nameof(Index))
                    : RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
            }

            TempData["Ok"] = repetido.CuentaPagada
                ? "Pago ya registrado. La cuenta está pagada."
                : $"Pago ya registrado. Saldo pendiente: {repetido.SaldoPendiente:C0}.";

            return repetido.CuentaPagada || acceso.FechaCierre.HasValue
                ? RedirectToAction(nameof(Index))
                : RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
        }

        if (acceso.FechaCierre.HasValue)
        {
            TempData["Error"] = "La mesa ya está cerrada y no admite nuevos pagos.";
            return RedirectToAction(nameof(Index));
        }

        var detalles = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesionId == modelo.SesionId &&
                d.Pedido.Estado != EstadoPedido.Cancelado &&
                d.Estado != EstadoDetallePedido.Cancelado)
            .Select(d => new
            {
                d.Id,
                d.Subtotal,
                d.Pedido!.ComensalId
            })
            .ToListAsync();

        var pagadoPorDetalle = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd =>
                pd.Pago!.Cuenta!.MesaSesionId == modelo.SesionId &&
                pd.Pago.Estado == EstadoPago.Confirmado)
            .GroupBy(pd => pd.DetallePedidoId)
            .Select(g => new
            {
                DetalleId = g.Key,
                Monto = g.Sum(x => x.MontoAsignado)
            })
            .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

        var pendientes = detalles
            .Select(d => new SaldoDetallePago(
                d.Id,
                d.ComensalId,
                Math.Max(0, d.Subtotal - pagadoPorDetalle.GetValueOrDefault(d.Id))))
            .Where(d => d.Saldo > 0)
            .ToList();

        var modo = (modelo.Modo ?? string.Empty).Trim();
        List<AsignacionPagoInput> asignaciones;
        int? comensalPago = null;

        switch (modo)
        {
            case "Todo":
                asignaciones = pendientes
                    .Select(d => new AsignacionPagoInput(d.DetalleId, d.Saldo))
                    .ToList();
                break;

            case "Igual":
                if (modelo.CantidadPartes < 1 || modelo.CantidadPartes > 20)
                {
                    TempData["Error"] = "La cantidad de partes debe estar entre 1 y 20.";
                    return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
                }

                var totalCuenta = detalles.Sum(d => d.Subtotal);
                var montoParte = Math.Ceiling(totalCuenta / modelo.CantidadPartes);
                var objetivo = Math.Min(montoParte, pendientes.Sum(d => d.Saldo));
                asignaciones = ConstruirAsignacionesHastaMonto(pendientes, objetivo);
                break;

            case "PorConsumo":
                if (!modelo.ComensalId.HasValue ||
                    detalles.All(d => d.ComensalId != modelo.ComensalId.Value))
                {
                    TempData["Error"] = "Selecciona un comensal válido.";
                    return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
                }

                comensalPago = modelo.ComensalId.Value;
                asignaciones = pendientes
                    .Where(d => d.ComensalId == comensalPago.Value)
                    .Select(d => new AsignacionPagoInput(d.DetalleId, d.Saldo))
                    .ToList();
                break;

            case "Seleccion":
            case "Personalizado":
                var ids = modelo.DetallePedidoIds.Distinct().ToHashSet();
                if (ids.Count == 0 ||
                    ids.Any(id => pendientes.All(d => d.DetalleId != id)))
                {
                    TempData["Error"] = "Selecciona consumos pendientes válidos para registrar el pago.";
                    return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
                }

                asignaciones = pendientes
                    .Where(d => ids.Contains(d.DetalleId))
                    .Select(d => new AsignacionPagoInput(d.DetalleId, d.Saldo))
                    .ToList();
                break;

            default:
                TempData["Error"] = "Modo de división inválido.";
                return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
        }

        if (asignaciones.Count == 0)
        {
            TempData["Error"] = "No quedan consumos pendientes para este pago.";
            return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
        }

        var resultado = await _cuentaService.RegistrarPagoConfirmadoAsync(
            modelo.SesionId,
            comensalPago,
            asignaciones,
            modelo.Metodo,
            modelo.Propina,
            modelo.IdempotencyKey);

        if (!resultado.Ok)
        {
            TempData["Error"] = string.Join(" ", resultado.Errores);
            return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
        }

        if (resultado.CuentaPagada)
        {
            TempData["Ok"] = "Cuenta pagada completamente. La mesa quedó liberada.";
            return RedirectToAction("Index", "Mesas");
        }

        TempData["Ok"] = $"Pago registrado. Saldo pendiente: {resultado.SaldoPendiente:C0}.";
        return RedirectToAction(nameof(Cuenta), new { id = modelo.SesionId });
    }

    private static List<AsignacionPagoInput> ConstruirAsignacionesHastaMonto(
        IEnumerable<SaldoDetallePago> pendientes,
        decimal montoObjetivo)
    {
        var restante = montoObjetivo;
        var resultado = new List<AsignacionPagoInput>();

        foreach (var detalle in pendientes.OrderBy(d => d.DetalleId))
        {
            if (restante <= 0)
                break;

            var monto = Math.Min(detalle.Saldo, restante);
            if (monto <= 0)
                continue;

            resultado.Add(new AsignacionPagoInput(detalle.DetalleId, monto));
            restante -= monto;
        }

        return resultado;
    }

    private async Task<PedidosIndexViewModel> ConstruirOperacionAsync(int? sesionId = null)
    {
        var userId = GetUserId();

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .AsSplitQuery()
            .Where(s =>
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == RestauranteId &&
                (!sesionId.HasValue || s.Id == sesionId.Value) &&
                (User.EsAdmin() || s.GarzonId == userId || s.Mesa.GarzonId == userId))
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Historial)
                        .ThenInclude(h => h.Usuario)
            .OrderBy(s => s.Mesa!.Numero)
            .ToListAsync();

        var solicitudesPendientes = await ObtenerSolicitudesPendientesAsync();

        if (sesionId.HasValue)
            solicitudesPendientes = solicitudesPendientes.Where(s => s.SesionId == sesionId.Value).ToList();

        return new PedidosIndexViewModel
        {
            SolicitudesPendientes = solicitudesPendientes,
            Mesas = sesiones.Select(s => new PedidoMesaViewModel
            {
                SesionId = s.Id,
                MesaId = s.MesaId,
                MesaNumero = s.Mesa!.Numero,
                CuentaSolicitada = s.CuentaSolicitadaEn.HasValue,
                Pedidos = s.Comensales
                    .SelectMany(comensal => comensal.Pedidos.Select(p => new PedidoResumenViewModel
                    {
                        Id = p.Id,
                        ComensalNombre = comensal.Nombre,
                        Estado = p.Estado,
                        Total = p.Detalles
                            .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                            .Sum(d => d.Subtotal),
                        FechaCreacion = p.FechaCreacion,
                        Historial = p.Historial
                            .OrderBy(h => h.Fecha)
                            .Select(h => new HistorialPedidoResumenViewModel
                            {
                                EstadoAnterior = h.EstadoAnterior,
                                EstadoNuevo = h.EstadoNuevo,
                                Fecha = h.Fecha,
                                Actor = h.Usuario != null ? h.Usuario.NombreCompleto : "Sistema"
                            })
                            .ToList(),
                        Detalles = p.Detalles.Select(d => new DetallePedidoResumenViewModel
                        {
                            Id = d.Id,
                            Estado = d.Estado,
                            EstacionId = d.EstacionId,
                            Estacion = d.EstacionNombre,
                            FechaInicioPreparacion = d.FechaInicioPreparacion,
                            FechaListo = d.FechaListo,
                            FechaEntregado = d.FechaEntregado,
                            Producto = d.NombreProducto,
                            Cantidad = d.Cantidad,
                            Subtotal = d.Subtotal,
                            Observacion = d.Observacion,
                            Personalizaciones = d.Ingredientes
                                .Select(i =>
                                    $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} " +
                                    $"{i.NombreIngrediente}" +
                                    (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
                                .ToList()
                        }).ToList()
                    }))
                    .OrderByDescending(p => p.FechaCreacion)
                    .ToList()
            })
            .Where(m => m.Pedidos.Count > 0 || m.CuentaSolicitada || sesionId.HasValue)
            .ToList()
        };
    }

    private sealed record SaldoDetallePago(
        int DetalleId,
        int ComensalId,
        decimal Saldo);
    private Task<List<SolicitudMesaResumenViewModel>> ObtenerSolicitudesPendientesAsync()
    {
        var userId = GetUserId();

        return _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.Estado == EstadoSolicitudMesa.Pendiente &&
                s.MesaSesion!.FechaCierre == null &&
                s.MesaSesion.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.MesaSesion.GarzonId == userId ||
                 s.MesaSesion.Mesa.GarzonId == userId))
            .OrderBy(s => s.FechaCreacion)
            .Select(s => new SolicitudMesaResumenViewModel
            {
                Id = s.Id,
                SesionId = s.MesaSesionId,
                MesaNumero = s.MesaSesion!.Mesa!.Numero,
                ComensalNombre = s.Comensal!.Nombre,
                Tipo = s.Tipo,
                Mensaje = s.Mensaje,
                Estado = s.Estado,
                FechaCreacion = s.FechaCreacion,
                FechaResolucion = s.FechaResolucion
            })
            .ToListAsync();
    }


}