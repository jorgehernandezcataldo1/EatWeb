using EatWeb.Data;
using EatWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetRestauranteId()
    {
        return int.Parse(User.FindFirst("RestauranteId")?.Value ?? "0");
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var restauranteId = GetRestauranteId();

        var stats = new
        {
            TotalProductos = await _context.Productos
                .Where(p => p.RestauranteId == restauranteId)
                .CountAsync(),
            TotalMesas = await _context.Mesas
                .Where(m => m.RestauranteId == restauranteId)
                .CountAsync(),
            TotalPedidosHoy = await _context.Pedidos
                .Where(p => p.MesaSesion!.Mesa!.RestauranteId == restauranteId && p.FechaCreacion.Date == DateTime.Now.Date)
                .CountAsync(),
            PedidosPendientes = await _context.Pedidos
                .Where(p => p.MesaSesion!.Mesa!.RestauranteId == restauranteId && p.Estado == "Pendiente")
                .CountAsync()
        };

        ViewBag.Stats = stats;
        return View();
    }
}
