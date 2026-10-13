using System.Security.Claims;
using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class PantallaController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;

    public PantallaController(ApplicationDbContext context, PedidoService pedidoService)
    {
        _context = context;
        _pedidoService = pedidoService;
    }

    // Página completa
    [HttpGet]
    public async Task<IActionResult> Index(int? estacionId)
    {
        return View(await ConstruirAsync(estacionId));
    }

    // Solo el tablero: lo pide AJAX cada 5 segundos
    [HttpGet]
    public async Task<IActionResult> Tablero(int? estacionId)
    {
        Response.Headers.CacheControl = "no-store";
        return PartialView("_Tablero", await ConstruirAsync(estacionId));
    }

    // Cocina/Bar solo pueden pasar un ítem a EnPreparacion o Listo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cambiar(int id, string nuevoEstado)
    {
        if (nuevoEstado != EstadoDetallePedido.EnPreparacion &&
            nuevoEstado != EstadoDetallePedido.Listo)
            return BadRequest("Estado no permitido.");

        // El ítem debe ser de MI restaurante y de una mesa abierta
        var existe = await _context.DetallesPedidos
            .AsNoTracking()
            .AnyAsync(d =>
                d.Id == id &&
                d.Pedido!.MesaSesion!.Mesa!.RestauranteId == RestauranteId &&
                d.Pedido.MesaSesion.FechaCierre == null);

        if (!existe)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var resultado = await _pedidoService.CambiarEstadoDetalleAsync(id, nuevoEstado, userId);

        return resultado.Ok
            ? Ok()
            : BadRequest(string.Join(" ", resultado.Errores));
    }

    private async Task<PantallaViewModel> ConstruirAsync(int? estacionId)
    {
        var restauranteId = RestauranteId;

        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == restauranteId && e.Activa)
            .OrderBy(e => e.Orden)
            .ThenBy(e => e.Nombre)
            .Select(e => new { e.Id, e.Nombre })
            .ToListAsync();

        // Si piden una estación que no es de este restaurante, mostramos "Todas"
        if (estacionId.HasValue && estaciones.All(e => e.Id != estacionId.Value))
            estacionId = null;

        // Ítems vivos: mesa abierta, pedido no cancelado, ítem sin entregar/cancelar
        var activos = _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesion!.Mesa!.RestauranteId == restauranteId &&
                d.Pedido.MesaSesion.FechaCierre == null &&
                d.Pedido.Estado != EstadoPedido.Cancelado &&
                (d.Estado == EstadoDetallePedido.Pendiente ||
                 d.Estado == EstadoDetallePedido.EnPreparacion ||
                 d.Estado == EstadoDetallePedido.Listo));

        // Contadores de las pestañas (pendientes + en preparación por estación)
        var porAtender = await activos
            .Where(d => d.Estado != EstadoDetallePedido.Listo)
            .GroupBy(d => d.EstacionId)
            .Select(g => new { EstacionId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.EstacionId, x => x.Cantidad);

        if (estacionId.HasValue)
            activos = activos.Where(d => d.EstacionId == estacionId.Value);

        var datos = await activos
            .OrderBy(d => d.Pedido!.FechaCreacion)
            .Select(d => new
            {
                d.Id,
                MesaNumero = d.Pedido!.MesaSesion!.Mesa!.Numero,
                Comensal = d.Pedido.Comensal!.Nombre,
                Estacion = d.EstacionNombre,
                Producto = d.NombreProducto,
                d.Cantidad,
                d.Observacion,
                NotaPedido = d.Pedido.ObservacionGeneral,
                d.Estado,
                Fecha = d.Pedido.FechaCreacion,
                d.FechaListo,
                Ingredientes = d.Ingredientes
                    .Select(i => new { i.Accion, i.NombreIngrediente })
                    .ToList()
            })
            .ToListAsync();

        var ahora = DateTime.UtcNow;

        var items = datos.Select(d => new
        {
            d.Estado,
            d.FechaListo,
            Vm = new PantallaItemViewModel
            {
                DetalleId = d.Id,
                MesaNumero = d.MesaNumero,
                Comensal = d.Comensal,
                Estacion = d.Estacion,
                Producto = d.Producto,
                Cantidad = d.Cantidad,
                Observacion = d.Observacion,
                NotaPedido = d.NotaPedido,
                Minutos = Math.Max(0, (int)(ahora - d.Fecha).TotalMinutes),
                Personalizaciones = d.Ingredientes
                    .Select(i => (i.Accion == AccionIngrediente.Quitar ? "Sin " : "Agregar ") + i.NombreIngrediente)
                    .ToList()
            }
        }).ToList();

        return new PantallaViewModel
        {
            EstacionId = estacionId,
            Titulo = estacionId.HasValue
                ? estaciones.First(e => e.Id == estacionId.Value).Nombre
                : "Todas las estaciones",
            TotalPorAtender = porAtender.Values.Sum(),
            Tabs = estaciones.Select(e => new PantallaTabViewModel
            {
                Id = e.Id,
                Nombre = e.Nombre,
                PorAtender = porAtender.GetValueOrDefault(e.Id)
            }).ToList(),
            Pendientes = items.Where(i => i.Estado == EstadoDetallePedido.Pendiente).Select(i => i.Vm).ToList(),
            EnPreparacion = items.Where(i => i.Estado == EstadoDetallePedido.EnPreparacion).Select(i => i.Vm).ToList(),
            Listos = items.Where(i => i.Estado == EstadoDetallePedido.Listo)
                          .OrderBy(i => i.FechaListo).Select(i => i.Vm).ToList()
        };
    }
}