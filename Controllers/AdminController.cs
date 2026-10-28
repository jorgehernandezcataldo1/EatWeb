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
        if (!await _acceso.PuedeAdministrarAsync(id))
            return Forbid();

        var seleccionado = await _restauranteContext.SeleccionarAsync(id);
        if (!seleccionado)
            return Forbid();

        return RedirectToAction(nameof(Restaurante), new { id });
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var restaurantes = await _acceso.ObtenerRestaurantesAdministrablesAsync();

        if (!restaurantes.Any())
            return Forbid();

        if (restaurantes.Count == 1)
            return RedirectToAction(nameof(Restaurante), new { id = restaurantes[0].Id });

        var model = await _dashboard.ObtenerCadenaAsync(
            restaurantes.Select(r => r.Id).ToList());

        return View("IndexCadena", model);
    }

    [HttpGet("Restaurante/{id:int}")]
    public async Task<IActionResult> Restaurante(int id)
    {
        if (!await _acceso.PuedeAdministrarAsync(id))
            return Forbid();

        var model = await _dashboard.ObtenerRestauranteAsync(id);
        if (model == null)
            return NotFound();

        if (!await _restauranteContext.SeleccionarAsync(id))
            return Forbid();

        ViewData["RestauranteNombre"] = model.RestauranteNombre;
        return View("Restaurante", model);
    }
}
