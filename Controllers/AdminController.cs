using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AccesoRestauranteService _acceso;
    private readonly RestauranteContextService _restauranteContext;


    public AdminController(
        ApplicationDbContext context,
        AccesoRestauranteService acceso,
        RestauranteContextService restauranteContext)
    {
        _context = context;
        _acceso = acceso;
        _restauranteContext = restauranteContext;
    }

    [HttpGet("SeleccionarRestaurante/{id:int}")]
    public async Task<IActionResult> SeleccionarRestaurante(int id)
    {
        var seleccionado = await _restauranteContext.SeleccionarAsync(id);

        if (!seleccionado)
            return Forbid();

        return RedirectToAction(nameof(Restaurante), new { id });
    }


    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var restaurantes = await _acceso
            .ObtenerRestaurantesAccesiblesAsync();

        if (!restaurantes.Any())
            return Forbid();

        // Si es administrador de restaurante,
        // tiene un solo restaurante.
        if (User.IsInRole(Roles.AdminRestaurante))
        {
            var restaurante = restaurantes.SingleOrDefault();

            if (restaurante == null)
                return Forbid();

            return RedirectToAction(
                nameof(Restaurante),
                new { id = restaurante.Id });
        }

        // AdminCadena
        var datos = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => restaurantes.Select(x => x.Id).Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.Nombre,
                TotalProductos = r.Productos.Count(),
                TotalMesas = r.Mesas.Count(),
                TotalPedidos = r.Mesas
                    .SelectMany(m => m.Sesiones)
                    .SelectMany(s => s.Pedidos)
                    .Count()
            })
            .OrderBy(r => r.Nombre)
            .ToListAsync();

        return View("IndexCadena", datos);
    }

    [HttpGet("Restaurante/{id:int}")]
    public async Task<IActionResult> Restaurante(int id)
    {
        if (!await _acceso.PuedeAccederAsync(id))
            return Forbid();

        var inicioHoy = ZonaHorariaService.InicioDelDiaUtc();

        var stats = new
        {
            TotalProductos = await _context.Productos
                .CountAsync(p => p.RestauranteId == id),

            TotalMesas = await _context.Mesas
                .CountAsync(m => m.RestauranteId == id),

            TotalPedidosHoy = await _context.Pedidos
                .CountAsync(p =>
                    p.MesaSesion!.Mesa!.RestauranteId == id &&
                    p.FechaCreacion >= inicioHoy),

            PedidosPendientes = await _context.Pedidos
                .CountAsync(p =>
                    p.MesaSesion!.Mesa!.RestauranteId == id &&
                    p.Estado == "Pendiente")
        };

        var restaurante = await _context.Restaurantes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (restaurante == null)
            return NotFound();

        ViewBag.Restaurante = restaurante;
        ViewBag.Stats = stats;

        return View("Restaurante", restaurante);
    }
}
