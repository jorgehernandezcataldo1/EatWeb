using EatWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace EatWeb.ViewComponents;

public class RestauranteSelectorViewComponent : ViewComponent
{
    private readonly AccesoRestauranteService _acceso;
    private readonly RestauranteContextService _contexto;

    public RestauranteSelectorViewComponent(
        AccesoRestauranteService acceso,
        RestauranteContextService contexto)
    {
        _acceso = acceso;
        _contexto = contexto;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var restaurantes = await _acceso.ObtenerRestaurantesAccesiblesAsync();
        var actual = await _contexto.ObtenerActualAsync();

        return View(new RestauranteSelectorViewModel
        {
            RestauranteActualId = actual?.Id,
            RestauranteActualNombre = actual?.Nombre,
            Restaurantes = restaurantes.Select(r => new RestauranteSelectorItemViewModel
            {
                Id = r.Id,
                Nombre = r.Nombre
            }).ToList()
        });
    }
}

public class RestauranteSelectorViewModel
{
    public int? RestauranteActualId { get; set; }
    public string? RestauranteActualNombre { get; set; }
    public List<RestauranteSelectorItemViewModel> Restaurantes { get; set; } = new();
}

public class RestauranteSelectorItemViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
