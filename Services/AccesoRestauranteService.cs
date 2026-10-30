using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Services;

/// <summary>
/// Resuelve acceso por restaurante usando dos dimensiones:
/// rol global de Identity + rol local de la membresía.
/// Nunca basta con tener un rol global para operar otro restaurante.
/// </summary>
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

    private async Task<(ApplicationUser Usuario, HashSet<string> Roles)?> ObtenerUsuarioActivoAsync()
    {
        if (string.IsNullOrWhiteSpace(UsuarioId))
            return null;

        var usuario = await _userManager.FindByIdAsync(UsuarioId);
        if (usuario == null || !usuario.Activo)
            return null;

        var roles = (await _userManager.GetRolesAsync(usuario))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (usuario, roles);
    }

    /// <summary>
    /// Restaurantes donde existe una combinación válida entre rol global y membresía local activa.
    /// </summary>
    public async Task<List<Restaurante>> ObtenerRestaurantesAccesiblesAsync()
    {
        var acceso = await ObtenerUsuarioActivoAsync();
        if (acceso == null)
            return new List<Restaurante>();

        var usuarioId = acceso.Value.Usuario.Id;
        var roles = acceso.Value.Roles;

        var esAdminCadena = roles.Contains(Roles.AdminCadena);
        var esAdminRestaurante = roles.Contains(Roles.AdminRestaurante);
        var esGarzon = roles.Contains(Roles.Garzon);
        var esEstacion = roles.Contains(Roles.Cocina) || roles.Contains(Roles.Bar);

        return await _context.Restaurantes
            .AsNoTracking()
            .Where(r =>
                r.Activo && (r.CadenaId == null || r.Cadena!.Activo) &&
                ((esAdminCadena &&
                 r.CadenaId != null &&
                 _context.CadenasMiembros.Any(cm =>
                     cm.UsuarioId == usuarioId &&
                     cm.CadenaId == r.CadenaId &&
                     (cm.Rol == RolCadena.Administrador || cm.Rol == RolCadena.Gerente)))
                ||
                (esAdminRestaurante &&
                 _context.RestaurantesMiembros.Any(rm =>
                     rm.UsuarioId == usuarioId &&
                     rm.RestauranteId == r.Id &&
                     rm.Activo &&
                     (rm.Rol == RolRestaurante.Administrador || rm.Rol == RolRestaurante.Encargado)))
                ||
                (esGarzon &&
                 _context.RestaurantesMiembros.Any(rm =>
                     rm.UsuarioId == usuarioId &&
                     rm.RestauranteId == r.Id &&
                     rm.Activo &&
                     rm.Rol == RolRestaurante.Garzon))
                ||
                (esEstacion &&
                 _context.RestaurantesMiembros.Any(rm =>
                     rm.UsuarioId == usuarioId &&
                     rm.RestauranteId == r.Id &&
                     rm.Activo &&
                     rm.Rol == RolRestaurante.Cocina))))
            .OrderBy(r => r.Nombre)
            .ToListAsync();
    }

    /// <summary>
    /// Solo restaurantes administrables. Se usa en el selector/dashboard de administración.
    /// </summary>
    public async Task<List<Restaurante>> ObtenerRestaurantesAdministrablesAsync()
    {
        var acceso = await ObtenerUsuarioActivoAsync();
        if (acceso == null)
            return new List<Restaurante>();

        var usuarioId = acceso.Value.Usuario.Id;
        var roles = acceso.Value.Roles;
        var esAdminCadena = roles.Contains(Roles.AdminCadena);
        var esAdminRestaurante = roles.Contains(Roles.AdminRestaurante);

        return await _context.Restaurantes
            .AsNoTracking()
            .Where(r =>
                r.Activo && (r.CadenaId == null || r.Cadena!.Activo) &&
                ((esAdminCadena &&
                 r.CadenaId != null &&
                 _context.CadenasMiembros.Any(cm =>
                     cm.UsuarioId == usuarioId &&
                     cm.CadenaId == r.CadenaId &&
                     (cm.Rol == RolCadena.Administrador || cm.Rol == RolCadena.Gerente)))
                ||
                (esAdminRestaurante &&
                 _context.RestaurantesMiembros.Any(rm =>
                     rm.UsuarioId == usuarioId &&
                     rm.RestauranteId == r.Id &&
                     rm.Activo &&
                     (rm.Rol == RolRestaurante.Administrador || rm.Rol == RolRestaurante.Encargado)))))
            .OrderBy(r => r.Nombre)
            .ToListAsync();
    }

    public Task<bool> PuedeAdministrarAsync(int restauranteId) =>
        PuedeAccederConRolesAsync(
            restauranteId,
            new[]
            {
                new[] { Roles.AdminCadena, Roles.AdminRestaurante }
            });

    /// <summary>
    /// Acceso general al restaurante con cualquier rol operativo válido del usuario.
    /// </summary>
    public Task<bool> PuedeAccederAsync(int restauranteId) =>
        PuedeAccederConRolesAsync(restauranteId, Array.Empty<string[]>());

    /// <summary>
    /// Valida los grupos de roles declarados por [Authorize].
    /// Cada atributo es un grupo AND; dentro de cada grupo sus roles son OR.
    /// Además del rol global, exige la membresía local correspondiente.
    /// </summary>
    public async Task<bool> PuedeAccederConRolesAsync(
        int restauranteId,
        IEnumerable<string[]> gruposRoles)
    {
        var acceso = await ObtenerUsuarioActivoAsync();
        if (acceso == null)
            return false;

        var usuarioId = acceso.Value.Usuario.Id;
        var rolesGlobales = acceso.Value.Roles;

        var restauranteActivo = await _context.Restaurantes
            .AsNoTracking()
            .AnyAsync(r => r.Id == restauranteId && r.Activo && (r.CadenaId == null || r.Cadena!.Activo));

        if (!restauranteActivo)
            return false;

        var membresiaLocal = await _context.RestaurantesMiembros
            .AsNoTracking()
            .Where(rm =>
                rm.RestauranteId == restauranteId &&
                rm.UsuarioId == usuarioId &&
                rm.Activo)
            .Select(rm => (RolRestaurante?)rm.Rol)
            .FirstOrDefaultAsync();

        var cadenaId = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => r.Id == restauranteId)
            .Select(r => r.CadenaId)
            .FirstOrDefaultAsync();

        RolCadena? membresiaCadena = null;
        if (cadenaId.HasValue)
        {
            membresiaCadena = await _context.CadenasMiembros
                .AsNoTracking()
                .Where(cm => cm.CadenaId == cadenaId.Value && cm.UsuarioId == usuarioId)
                .Select(cm => (RolCadena?)cm.Rol)
                .FirstOrDefaultAsync();
        }

        bool RolHabilitado(string rol) =>
            rolesGlobales.Contains(rol) &&
            rol switch
            {
                Roles.AdminCadena =>
                    membresiaCadena is RolCadena.Administrador or RolCadena.Gerente,

                Roles.AdminRestaurante =>
                    membresiaLocal is RolRestaurante.Administrador or RolRestaurante.Encargado,

                Roles.Garzon =>
                    membresiaLocal == RolRestaurante.Garzon,

                Roles.Cocina or Roles.Bar =>
                    membresiaLocal == RolRestaurante.Cocina,

                _ => false
            };

        var grupos = gruposRoles
            .Where(g => g.Length > 0)
            .ToList();

        if (grupos.Count == 0)
        {
            return rolesGlobales.Any(RolHabilitado);
        }

        foreach (var grupo in grupos)
        {
            if (!grupo.Any(RolHabilitado))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Obtiene un restaurante solamente si existe acceso válido.
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
