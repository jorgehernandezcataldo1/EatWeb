using System.Security.Claims;
using EatWeb.Data;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = "Admin,Garzon")]
public class PedidosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;

    public PedidosController(ApplicationDbContext context, PedidoService pedidoService)
    {
        _context = context;
        _pedidoService = pedidoService;
    }

    private int GetRestauranteId() =>
        int.Parse(User.FindFirst("RestauranteId")?.Value ?? "0");

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var restauranteId = GetRestauranteId();
        var userId = GetUserId();

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .Where(s =>
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId &&
                (User.IsInRole("Admin") || s.Mesa.GarzonId == userId))
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
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
                        Detalles = p.Detalles.Select(d => new DetallePedidoResumenViewModel
                        {
                            Producto = d.NombreProducto,
                            Cantidad = d.Cantidad,
                            Subtotal = d.Subtotal,
                            Observacion = d.Observacion,
                            Personalizaciones = d.Ingredientes
                                .Select(i => $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} {i.NombreIngrediente}" +
                                             (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
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
    public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado)
    {
        if (!EstadoPedido.Todos().Contains(nuevoEstado))
            return BadRequest();

        var pedido = await _context.Pedidos
            .Include(p => p.MesaSesion)
                .ThenInclude(s => s!.Mesa)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.MesaSesion!.Mesa!.RestauranteId == GetRestauranteId());

        if (pedido == null)
            return NotFound();

        if (User.IsInRole("Garzon") && pedido.MesaSesion!.Mesa!.GarzonId != GetUserId())
            return Forbid();

        var resultado = await _pedidoService.CambiarEstadoAsync(
            id,
            nuevoEstado,
            GetUserId() ?? string.Empty);

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Cuenta(int id)
    {
        var restauranteId = GetRestauranteId();
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
                s.Mesa!.RestauranteId == restauranteId &&
                (User.IsInRole("Admin") || s.Mesa.GarzonId == userId));

        if (sesion == null)
            return NotFound();

        var pedidos = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado != EstadoPedido.Cancelado)
            .ToList();

        var total = pedidos.Sum(p => p.Total);
        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(c => new CuentaPersonaViewModel
            {
                ComensalId = c.Id,
                Nombre = c.Nombre,
                TotalConsumido = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .Sum(p => p.Total),
                Items = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .SelectMany(p => p.Detalles.Select(d => $"{d.Cantidad}x {d.NombreProducto}"))
                    .ToList()
            })
            .ToList();

        var cantidadPersonas = Math.Max(personas.Count, 1);
        var baseIgual = Math.Floor(total / cantidadPersonas);
        var diferencia = total - (baseIgual * cantidadPersonas);

        return View(new CuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            Total = total,
            TotalVerificado = personas.Sum(p => p.TotalConsumido),
            MontoIgualBase = baseIgual,
            MontoIgualUltimaPersona = baseIgual + diferencia,
            Personas = personas
        });
    }
}
