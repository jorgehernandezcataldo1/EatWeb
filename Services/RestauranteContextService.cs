using EatWeb.Models;
using Microsoft.AspNetCore.Identity;

namespace EatWeb.Services;

public class RestauranteContextService
{
    private const string SessionKey = "RestauranteSeleccionadoId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AccesoRestauranteService _acceso;
    private readonly UserManager<ApplicationUser> _userManager;

    public RestauranteContextService(
        IHttpContextAccessor httpContextAccessor,
        AccesoRestauranteService acceso,
        UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _acceso = acceso;
        _userManager = userManager;
    }

    private HttpContext HttpContext =>
        _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("HttpContext no disponible.");

    /// <summary>
    /// Restaurante actualmente seleccionado.
    /// </summary>
    public int? RestauranteId
    {
        get
        {
            var valor = HttpContext.Session.GetInt32(SessionKey);
            return valor;
        }
    }

    /// <summary>
    /// Selecciona un restaurante después de comprobar que el usuario
    /// tiene permiso para acceder a él.
    /// </summary>
    public async Task<bool> SeleccionarAsync(int restauranteId)
    {
        if (!await _acceso.PuedeAccederAsync(restauranteId))
            return false;

        HttpContext.Session.SetInt32(SessionKey, restauranteId);

        return true;
    }

    /// <summary>
    /// Obtiene el restaurante seleccionado y comprueba nuevamente
    /// que el usuario siga teniendo acceso.
    /// </summary>
    public async Task<Restaurante?> ObtenerActualAsync()
    {
        var id = RestauranteId;

        if (!id.HasValue)
            return null;

        if (!await _acceso.PuedeAccederAsync(id.Value))
        {
            Limpiar();
            return null;
        }

        return await _acceso.ObtenerSiTieneAccesoAsync(id.Value);
    }

    /// <summary>
    /// Devuelve el restaurante actual o lanza una excepción si no existe.
    /// Útil en servicios internos.
    /// </summary>
    public async Task<int> ObtenerIdActualAsync()
    {
        var restaurante = await ObtenerActualAsync();

        if (restaurante == null)
            throw new InvalidOperationException(
                "No hay un restaurante seleccionado.");

        return restaurante.Id;
    }

    public void Limpiar()
    {
        HttpContext.Session.Remove(SessionKey);
    }
}
