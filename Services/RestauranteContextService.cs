using EatWeb.Models;

namespace EatWeb.Services;

public class RestauranteContextService
{
    private const string SessionKey = "RestauranteSeleccionadoId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AccesoRestauranteService _acceso;

    public RestauranteContextService(
        IHttpContextAccessor httpContextAccessor,
        AccesoRestauranteService acceso)
    {
        _httpContextAccessor = httpContextAccessor;
        _acceso = acceso;
    }

    private HttpContext HttpContext =>
        _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("HttpContext no disponible.");

    /// <summary>Restaurante seleccionado en la sesión (sin validar acceso).</summary>
    public int? RestauranteId => HttpContext.Session.GetInt32(SessionKey);

    /// <summary>Selecciona un restaurante si el usuario tiene acceso a él.</summary>
    public async Task<bool> SeleccionarAsync(int restauranteId)
    {
        if (!await _acceso.PuedeAccederAsync(restauranteId))
            return false;

        HttpContext.Session.SetInt32(SessionKey, restauranteId);
        return true;
    }

    /// <summary>
    /// Restaurante actual. Siempre revalida el acceso.
    /// Si no hay selección y el usuario solo tiene UN restaurante (admin de restaurante,
    /// garzón o cadena con uno solo), se selecciona automáticamente.
    /// Si tiene varios (admin de cadena), devuelve null hasta que elija uno.
    /// </summary>
    public async Task<Restaurante?> ObtenerActualAsync()
    {
        var id = RestauranteId;

        if (id.HasValue)
        {
            var actual = await _acceso.ObtenerSiTieneAccesoAsync(id.Value);
            if (actual != null)
                return actual;

            Limpiar();
        }

        var accesibles = await _acceso.ObtenerRestaurantesAccesiblesAsync();
        if (accesibles.Count == 1)
        {
            HttpContext.Session.SetInt32(SessionKey, accesibles[0].Id);
            return accesibles[0];
        }

        return null;
    }

    public async Task<int> ObtenerIdActualAsync()
    {
        var restaurante = await ObtenerActualAsync();

        if (restaurante == null)
            throw new InvalidOperationException("No hay un restaurante seleccionado.");

        return restaurante.Id;
    }

    public void Limpiar()
    {
        HttpContext.Session.Remove(SessionKey);
    }
}