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

[Authorize(Roles = Roles.Estacion)]
public class EstacionTrabajoController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;

    public EstacionTrabajoController(ApplicationDbContext context, PedidoService pedidoService)
    {
        _context = context;
        _pedidoService = pedidoService;
    }

    private string EstacionNombre => User.IsInRole(Roles.Bar) ? "Bar" : "Cocina";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Response.Headers.CacheControl = "no-store";
        var estacion = EstacionNombre;

        var items = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesion!.FechaCierre == null &&
                d.Pedido.MesaSesion.Mesa!.RestauranteId == RestauranteId &&
                d.EstacionNombre == estacion &&
                d.Estado != EstadoDetallePedido.Entregado &&
                d.Estado != EstadoDetallePedido.Cancelado)
            .OrderBy(d => d.FechaInicioPreparacion ?? d.Pedido!.FechaCreacion)
            .Select(d => new EstacionTrabajoItemViewModel
            {
                DetalleId = d.Id,
                MesaNumero = d.Pedido!.MesaSesion!.Mesa!.Numero,
                PedidoId = d.PedidoId,
                Producto = d.NombreProducto,
                Cantidad = d.Cantidad,
                Estado = d.Estado,
                Observacion = d.Observacion,
                FechaPedido = d.Pedido.FechaCreacion,
                Personalizaciones = d.Ingredientes
                    .Select(i => i.Accion + " " + i.NombreIngrediente)
                    .ToList()
            })
            .ToListAsync();

        return View(new EstacionTrabajoViewModel
        {
            Estacion = estacion,
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado)
    {
        if (nuevoEstado != EstadoDetallePedido.EnPreparacion &&
            nuevoEstado != EstadoDetallePedido.Listo)
            return BadRequest();

        var estacion = EstacionNombre;

        var existe = await _context.DetallesPedidos
            .AsNoTracking()
            .AnyAsync(d =>
                d.Id == id &&
                d.EstacionNombre == estacion &&
                d.Pedido!.MesaSesion!.Mesa!.RestauranteId == RestauranteId);

        if (!existe)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var resultado = await _pedidoService.CambiarEstadoDetalleAsync(id, nuevoEstado, userId);

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);

        return RedirectToAction(nameof(Index));
    }
}
