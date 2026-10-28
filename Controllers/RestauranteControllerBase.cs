using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace EatWeb.Controllers;

/// <summary>
/// Base para controllers que trabajan dentro de un restaurante.
/// Revalida tanto la selección de restaurante como el rol de la membresía local
/// exigido por los atributos [Authorize] del controller/acción.
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

        var acceso = context.HttpContext.RequestServices
            .GetRequiredService<AccesoRestauranteService>();

        var restaurante = await restauranteContext.ObtenerActualAsync();

        if (restaurante == null)
        {
            context.Result = User.EsAdmin()
                ? RedirectToAction("Index", "Admin")
                : Forbid();
            return;
        }

        var gruposRoles = ObtenerGruposRoles(context);

        if (!await acceso.PuedeAccederConRolesAsync(restaurante.Id, gruposRoles))
        {
            context.Result = Forbid();
            return;
        }

        RestauranteId = restaurante.Id;
        ViewData["RestauranteNombre"] = restaurante.Nombre;

        await next();
    }

    private static List<string[]> ObtenerGruposRoles(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return new List<string[]>();

        var atributos = descriptor.ControllerTypeInfo
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(descriptor.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        return atributos
            .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
            .Select(a => a.Roles!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(g => g.Length > 0)
            .ToList();
    }
}
