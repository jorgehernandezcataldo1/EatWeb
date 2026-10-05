using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.AdminOGarzon)]
public class PedidosController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;
    private readonly SesionService _sesionService;
    private readonly CuentaService _cuentaService;

    public PedidosController(
        ApplicationDbContext context,
        PedidoService pedidoService,
        SesionService sesionService,
        CuentaService cuentaService)
    {
        _context = context;
        _pedidoService = pedidoService;
        _sesionService = sesionService;
        _cuentaService = cuentaService;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .AsSplitQuery()
            .Where(s =>
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == RestauranteId &&
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

        var vm = new PedidosIndexViewModel
        {
            Mesas = sesiones.Select(s => new PedidoMesaViewModel
            {
                SesionId = s.Id,
                MesaId = s.MesaId,
                MesaNumero = s.Mesa!.Numero,
                CuentaSolicitada = s.CuentaSolicitadaEn.HasValue,

                Pedidos = s.Comensales
                    .SelectMany(c => c.Pedidos.Select(p => new PedidoResumenViewModel
                    {
                        Id = p.Id,
                        ComensalNombre = c.Nombre,
                        Estado = p.Estado,
                        Total = p.Total,
                        FechaCreacion = p.FechaCreacion,

                        Historial = p.Historial
                            .OrderBy(h => h.Fecha)
                            .Select(h => new HistorialPedidoResumenViewModel
                            {
                                EstadoAnterior = h.EstadoAnterior,
                                EstadoNuevo = h.EstadoNuevo,
                                Fecha = h.Fecha,
                                Actor = h.Usuario != null
                                    ? h.Usuario.NombreCompleto
                                    : "Sistema"
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
                                    (i.PrecioExtra > 0
                                        ? $" (+{i.PrecioExtra:C0})"
                                        : ""))
                                .ToList()

                        }).ToList()

                    }))
                    .OrderByDescending(p => p.FechaCreacion)
                    .ToList()

            })
            .Where(m => m.Pedidos.Count > 0 || m.CuentaSolicitada)
            .ToList()
        };

        return View(vm);
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoDetalle(int id, string nuevoEstado)
    {
        if (!EstadoDetallePedido.Todos().Contains(nuevoEstado)) return BadRequest();

        var permitido = await _context.DetallesPedidos
            .AnyAsync(d => d.Id == id &&
                           d.Pedido!.MesaSesion!.Mesa!.RestauranteId == RestauranteId &&
                           (User.EsAdmin() ||
                            d.Pedido.MesaSesion.GarzonId == GetUserId() ||
                            d.Pedido.MesaSesion.Mesa.GarzonId == GetUserId()));

        if (!permitido) return NotFound();

        var resultado = await _pedidoService.CambiarEstadoDetalleAsync(id, nuevoEstado, GetUserId() ?? string.Empty);
        if (!resultado.Ok) TempData["Error"] = string.Join(" ", resultado.Errores);
        return RedirectToAction(nameof(Index));
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
                               e.RestauranteId == RestauranteId &&
                               e.Activa);

            if (!estacionValida) return BadRequest();
        }

        var resultado = await _pedidoService.CambiarEstadoMesaAsync(
            id,
            nuevoEstado,
            GetUserId() ?? string.Empty,
            estacionId);
        if (!resultado.Ok) TempData["Error"] = string.Join(" ", resultado.Errores);
        return RedirectToAction(nameof(Index));
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

        if (sesion == null) return NotFound();

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
        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> Cuenta(int id)
    {
        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .AsNoTracking()
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

        if (sesion == null) return NotFound();

        // Solo pedidos no cancelados
        var pedidosValidos = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado != EstadoPedido.Cancelado)
            .ToList();

        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(c =>
            {
                var pedidosComensal = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .ToList();

                return new CuentaPersonaViewModel
                {
                    ComensalId = c.Id,
                    Nombre = c.Nombre,
                    FechaIngreso = c.FechaIngreso,
                    TotalConsumido = pedidosComensal.Sum(p => p.Total),
                    Items = pedidosComensal
                        .SelectMany(p => p.Detalles.Select(d => new CuentaItemViewModel
                        {
                            PedidoId = p.Id,
                            DetallePedidoId = d.Id,
                            Producto = d.NombreProducto,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Subtotal = d.Subtotal,
                            Observacion = d.Observacion,
                            EstadoPedido = p.Estado,
                            Personalizaciones = d.Ingredientes
                                .Select(i => $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} {i.NombreIngrediente}"
                                    + (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
                                .ToList()
                        }))
                        .ToList()
                };
            })
            .ToList();

        var total = personas.Sum(p => p.TotalConsumido);

        return View(new CuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            FechaApertura = sesion.FechaApertura,
            CuentaSolicitada = sesion.CuentaSolicitadaEn.HasValue,
            CuentaSolicitadaEn = sesion.CuentaSolicitadaEn,
            Total = total,
            TotalVerificado = personas.Sum(p => p.TotalConsumido),
            Personas = personas,
            Items = personas.SelectMany(p => p.Items).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Dividir(int id)
    {
        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .AsNoTracking()
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.GarzonId == userId ||
                 s.Mesa.GarzonId == userId));

        if (sesion == null) return NotFound();

        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(c => new CuentaPersonaViewModel
            {
                ComensalId = c.Id,
                Nombre = c.Nombre,
                FechaIngreso = c.FechaIngreso,
                TotalConsumido = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .Sum(p => p.Total)
            })
            .ToList();

        var items = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado != EstadoPedido.Cancelado)
            .SelectMany(p => p.Detalles.Select(d => new CuentaItemViewModel
            {
                PedidoId = p.Id,
                DetallePedidoId = d.Id,
                Producto = d.NombreProducto,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Subtotal,
                EstadoPedido = p.Estado
            }))
            .ToList();

        var pagadoPorDetalle = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd => pd.Pago!.Cuenta!.MesaSesionId == sesion.Id &&
                         pd.Pago.Estado == EstadoPago.Confirmado)
            .GroupBy(pd => pd.DetallePedidoId)
            .Select(g => new { DetalleId = g.Key, Monto = g.Sum(x => x.MontoAsignado) })
            .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

        var total = items.Sum(i => i.Subtotal);
        var totalPagado = pagadoPorDetalle.Values.Sum();

        return View(new DivisionCuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            Total = total,
            TotalPagado = totalPagado,
            SaldoPendiente = Math.Max(0, total - totalPagado),
            Personas = personas,
            Items = items.Where(i => i.Subtotal > pagadoPorDetalle.GetValueOrDefault(i.DetallePedidoId)).ToList(),
            Modo = "Igual",
            CantidadPartes = personas.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarConDivision(CerrarConDivisionInputViewModel modelo)
    {
        var userId = GetUserId();

        var sesionValida = await _context.MesaSesiones
            .AsNoTracking()
            .AnyAsync(s => s.Id == modelo.SesionId &&
                           s.FechaCierre == null &&
                           s.Mesa!.RestauranteId == RestauranteId &&
                           (User.EsAdmin() ||
                            s.GarzonId == userId ||
                            s.Mesa.GarzonId == userId));

        if (!sesionValida) return NotFound();

        var detalles = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d => d.Pedido!.MesaSesionId == modelo.SesionId &&
                        d.Pedido.Estado != EstadoPedido.Cancelado)
            .Select(d => new { d.Id, d.Subtotal, d.Pedido!.ComensalId })
            .ToListAsync();

        IEnumerable<int> seleccionados = modelo.DetallePedidoIds;

        if (modelo.Modo == "PorConsumo" && modelo.ComensalId.HasValue)
            seleccionados = detalles.Where(d => d.ComensalId == modelo.ComensalId.Value).Select(d => d.Id);
        else if (modelo.Modo == "Todo")
            seleccionados = detalles.Select(d => d.Id);

        var ids = seleccionados.Distinct().ToHashSet();
        if (ids.Count == 0)
        {
            TempData["Error"] = "Selecciona al menos un consumo para registrar el pago.";
            return RedirectToAction(nameof(Dividir), new { id = modelo.SesionId });
        }

        var yaPagado = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd => ids.Contains(pd.DetallePedidoId) &&
                         pd.Pago!.Estado == EstadoPago.Confirmado)
            .GroupBy(pd => pd.DetallePedidoId)
            .Select(g => new { DetalleId = g.Key, Monto = g.Sum(x => x.MontoAsignado) })
            .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

        var asignaciones = detalles
            .Where(d => ids.Contains(d.Id))
            .Select(d => new AsignacionPagoInput(
                d.Id,
                Math.Max(0, d.Subtotal - yaPagado.GetValueOrDefault(d.Id))))
            .Where(a => a.Monto > 0)
            .ToList();

        var resultado = await _cuentaService.RegistrarPagoConfirmadoAsync(
            modelo.SesionId,
            modelo.ComensalId,
            asignaciones,
            string.IsNullOrWhiteSpace(modelo.Metodo) ? "Efectivo" : modelo.Metodo,
            modelo.Propina,
            modelo.IdempotencyKey);

        if (!resultado.Ok)
        {
            TempData["Error"] = string.Join(" ", resultado.Errores);
            return RedirectToAction(nameof(Dividir), new { id = modelo.SesionId });
        }

        if (resultado.CuentaPagada)
        {
            TempData["Ok"] = "Cuenta pagada completamente. La mesa quedó liberada.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Ok"] = $"Pago registrado. Saldo pendiente: {resultado.SaldoPendiente:C0}.";
        return RedirectToAction(nameof(Dividir), new { id = modelo.SesionId });
    }

}
