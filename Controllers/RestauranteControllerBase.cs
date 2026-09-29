using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EatWeb.Controllers;

/// <summary>
/// Base para los controllers que trabajan "dentro de un restaurante".
/// Antes de cada acción averigua cuál es el restaurante actual (y comprueba que el usuario
/// tenga acceso). Si no hay ninguno, manda al admin a elegir uno.
/// En las acciones usa simplemente la propiedad RestauranteId.
/// </summary>
public abstract class RestauranteControllerBase : Controller
{
    protected int RestauranteId { get; private set; }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var restauranteContext = context.HttpContext.RequestServices
            .GetRequiredService<RestauranteContextService>();

        var restaurante = await restauranteContext.ObtenerActualAsync();

        if (restaurante == null)
        {
            context.Result = User.EsAdmin()
                ? RedirectToAction("Index", "Admin")
                : Forbid();
            return;
        }

        RestauranteId = restaurante.Id;
        ViewData["RestauranteNombre"] = restaurante.Nombre;

        await next();
    }
}