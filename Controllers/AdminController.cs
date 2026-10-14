using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly AccesoRestauranteService _acceso;
    private readonly RestauranteContextService _restauranteContext;
    private readonly AdminDashboardService _dashboard;

    public AdminController(
        AccesoRestauranteService acceso,
        RestauranteContextService restauranteContext,
        AdminDashboardService dashboard)
    {
        _acceso = acceso;
        _restauranteContext = restauranteContext;
        _dashboard = dashboard;
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
        var restaurantes = await _acceso.ObtenerRestaurantesAccesiblesAsync();

        if (!restaurantes.Any())
            return Forbid();

        if (User.IsInRole(Roles.AdminRestaurante))
        {
            var restaurante = restaurantes.SingleOrDefault();
            if (restaurante == null)
                return Forbid();

            return RedirectToAction(nameof(Restaurante), new { id = restaurante.Id });
        }

        var model = await _dashboard.ObtenerCadenaAsync(
            restaurantes.Select(r => r.Id).ToList());

        return View("IndexCadena", model);
    }

    [HttpGet("Restaurante/{id:int}")]
    public async Task<IActionResult> Restaurante(int id)
    {
        if (!await _acceso.PuedeAccederAsync(id))
            return Forbid();

        var model = await _dashboard.ObtenerRestauranteAsync(id);
        if (model == null)
            return NotFound();

        await _restauranteContext.SeleccionarAsync(id);
        ViewData["RestauranteNombre"] = model.RestauranteNombre;

        return View("Restaurante", model);
    }
}
