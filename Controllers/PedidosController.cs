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
    public async Task<IActionResult> Index(int? sesionId = null)
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

        var vm = new PedidosIndexViewModel
        {
            SolicitudesPendientes = solicitudesPendientes,
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

    [HttpGet]
    public async Task<IActionResult> SolicitudesPendientes()
    {
        Response.Headers.CacheControl = "no-store";
        var solicitudes = await ObtenerSolicitudesPendientesAsync();
        return PartialView("_SolicitudesPendientes", solicitudes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtenderSolicitud(int id)
    {
        var resultado = await _solicitudMesaService.AtenderAsync(
            id,
            RestauranteId,
            GetUserId() ?? string.Empty,
            User.EsAdmin());

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);
        else
            TempData["Ok"] = "Solicitud marcada como atendida.";

        return RedirectToAction(nameof(Index));
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
                               e.RestauranteId == RestauranteId);

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