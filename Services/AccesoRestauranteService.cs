using EatWeb.Data;
using EatWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Services;

public class AccesoRestauranteService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccesoRestauranteService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    private string? UsuarioId =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Devuelve todos los restaurantes a los que el usuario tiene acceso.
    ///
    /// AdminCadena:
    ///     todos los restaurantes de sus cadenas.
    ///
    /// AdminRestaurante / Garzon:
    ///     solo los restaurantes donde tienen RestauranteMiembro.
    /// </summary>
    public async Task<List<Restaurante>> ObtenerRestaurantesAccesiblesAsync()
    {
        if (string.IsNullOrEmpty(UsuarioId))
            return new List<Restaurante>();

        var user = await _userManager.FindByIdAsync(UsuarioId);

        if (user == null || !user.Activo)
            return new List<Restaurante>();

        var roles = await _userManager.GetRolesAsync(user);

        // Administrador de cadena
        if (roles.Contains(Roles.AdminCadena))
        {
            return await _context.Restaurantes
                .AsNoTracking()
                .Where(r =>
                    r.CadenaId != null &&
                    _context.CadenasMiembros.Any(cm =>
                        cm.UsuarioId == UsuarioId &&
                        cm.CadenaId == r.CadenaId))
                .OrderBy(r => r.Nombre)
                .ToListAsync();
        }

        // Administrador de restaurante o garzón
        return await _context.Restaurantes
            .AsNoTracking()
            .Where(r =>
                _context.RestaurantesMiembros.Any(rm =>
                    rm.UsuarioId == UsuarioId &&
                    rm.RestauranteId == r.Id))
            .OrderBy(r => r.Nombre)
            .ToListAsync();
    }

    /// <summary>
    /// Comprueba si el usuario puede acceder a un restaurante concreto.
    /// </summary>
    public async Task<bool> PuedeAccederAsync(int restauranteId)
    {
        if (string.IsNullOrEmpty(UsuarioId))
            return false;

        var user = await _userManager.FindByIdAsync(UsuarioId);

        if (user == null || !user.Activo)
            return false;

        var roles = await _userManager.GetRolesAsync(user);

        // Admin de cadena:
        // puede acceder a cualquier restaurante perteneciente
        // a una cadena de la que sea miembro.
        if (roles.Contains(Roles.AdminCadena))
        {
            return await _context.Restaurantes
                .AnyAsync(r =>
                    r.Id == restauranteId &&
                    r.CadenaId != null &&
                    _context.CadenasMiembros.Any(cm =>
                        cm.UsuarioId == UsuarioId &&
                        cm.CadenaId == r.CadenaId));
        }

        // Admin de restaurante / Garzón:
        // solamente su RestauranteMiembro.
        return await _context.RestaurantesMiembros
            .AnyAsync(rm =>
                rm.UsuarioId == UsuarioId &&
                rm.RestauranteId == restauranteId);
    }

    /// <summary>
    /// Obtiene un restaurante concreto solamente si
    /// el usuario tiene permiso para acceder a él.
    /// </summary>
    public async Task<Restaurante?> ObtenerSiTieneAccesoAsync(int restauranteId)
    {
        if (!await PuedeAccederAsync(restauranteId))
            return null;

        return await _context.Restaurantes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == restauranteId);
    }
}
