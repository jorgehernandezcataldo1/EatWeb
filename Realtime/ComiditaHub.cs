using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Realtime;

public static class RealtimeGroups
{
    public static string Restaurante(int restauranteId) => $"restaurante:{restauranteId}";
    public static string Garzon(int restauranteId, string usuarioId) => $"garzon:{restauranteId}:{usuarioId}";
    public static string Estacion(int restauranteId, string estacion) =>
        $"estacion:{restauranteId}:{estacion.Trim().ToLowerInvariant()}";
    public static string Sesion(int sesionId) => $"sesion:{sesionId}";
}

public class ComiditaHub : Hub
{
    private const string CookieComensal = "eatweb_comensal";
    private readonly ApplicationDbContext _context;
    private readonly RealtimeConnectionRegistry _registry;

    public ComiditaHub(ApplicationDbContext context, RealtimeConnectionRegistry registry)
    {
        _context = context;
        _registry = registry;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.Unregister(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();

        // Cliente QR: la cookie solo habilita grupo si el token existe y toda la
        // cadena sesión -> mesa -> restaurante sigue activa.
        if (http?.Request.Cookies.TryGetValue(CookieComensal, out var valor) == true &&
            Guid.TryParse(valor, out var token))
        {
            var sesionId = await _context.Comensales
                .AsNoTracking()
                .Where(c =>
                    c.Token == token &&
                    c.MesaSesion!.FechaCierre == null &&
                    c.MesaSesion.Mesa!.Activa &&
                    c.MesaSesion.Mesa.Restaurante!.Activo &&
                    (c.MesaSesion.Mesa.Restaurante.CadenaId == null || c.MesaSesion.Mesa.Restaurante.Cadena!.Activo))
                .Select(c => (int?)c.MesaSesionId)
                .FirstOrDefaultAsync();

            if (sesionId.HasValue)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    RealtimeGroups.Sesion(sesionId.Value));
            }
        }

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var usuarioId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(usuarioId))
            {
                var usuarioActivo = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == usuarioId && u.Activo);

                if (!usuarioActivo)
                {
                    Context.Abort();
                    return;
                }

                var membresias = await _context.RestaurantesMiembros
                    .AsNoTracking()
                    .Where(rm =>
                        rm.UsuarioId == usuarioId &&
                        rm.Activo &&
                        rm.Restaurante.Activo &&
                        (rm.Restaurante.CadenaId == null || rm.Restaurante.Cadena!.Activo))
                    .Select(rm => new
                    {
                        rm.RestauranteId,
                        rm.Rol
                    })
                    .ToListAsync();

                var restaurantes = new HashSet<int>();

                if (Context.User.IsInRole(Roles.AdminRestaurante))
                {
                    foreach (var membresia in membresias.Where(m =>
                                 m.Rol == RolRestaurante.Administrador ||
                                 m.Rol == RolRestaurante.Encargado))
                    {
                        restaurantes.Add(membresia.RestauranteId);
                    }
                }

                if (Context.User.IsInRole(Roles.Garzon))
                {
                    foreach (var membresia in membresias.Where(m => m.Rol == RolRestaurante.Garzon))
                        restaurantes.Add(membresia.RestauranteId);
                }

                if (Context.User.IsInRole(Roles.Cocina) ||
                    Context.User.IsInRole(Roles.Bar))
                {
                    foreach (var membresia in membresias.Where(m => m.Rol == RolRestaurante.Cocina))
                        restaurantes.Add(membresia.RestauranteId);
                }

                if (Context.User.IsInRole(Roles.AdminCadena))
                {
                    var restaurantesCadena = await _context.Restaurantes
                        .AsNoTracking()
                        .Where(r =>
                            r.Activo && r.Cadena!.Activo &&
                            r.CadenaId != null &&
                            _context.CadenasMiembros.Any(cm =>
                                cm.UsuarioId == usuarioId &&
                                cm.CadenaId == r.CadenaId &&
                                (cm.Rol == RolCadena.Administrador ||
                                 cm.Rol == RolCadena.Gerente)))
                        .Select(r => r.Id)
                        .ToListAsync();

                    foreach (var restauranteId in restaurantesCadena)
                        restaurantes.Add(restauranteId);
                }

                var gruposPorRestaurante = restaurantes.ToDictionary(
                    id => id,
                    id => new List<string> { RealtimeGroups.Restaurante(id) });

                if (Context.User.IsInRole(Roles.Garzon))
                {
                    foreach (var restauranteId in membresias
                        .Where(m => m.Rol == RolRestaurante.Garzon)
                        .Select(m => m.RestauranteId))
                    {
                        gruposPorRestaurante[restauranteId].Add(
                            RealtimeGroups.Garzon(restauranteId, usuarioId));
                    }
                }

                var estaciones = membresias
                    .Where(m => m.Rol == RolRestaurante.Cocina)
                    .Select(m => m.RestauranteId)
                    .Distinct()
                    .ToList();

                if (Context.User.IsInRole(Roles.Cocina))
                {
                    foreach (var restauranteId in estaciones)
                    {
                        gruposPorRestaurante[restauranteId].Add(
                            RealtimeGroups.Estacion(restauranteId, "Cocina"));
                    }
                }

                if (Context.User.IsInRole(Roles.Bar))
                {
                    foreach (var restauranteId in estaciones)
                    {
                        gruposPorRestaurante[restauranteId].Add(
                            RealtimeGroups.Estacion(restauranteId, "Bar"));
                    }
                }
                foreach (var (restauranteId, grupos) in gruposPorRestaurante)
                {
                    // Registrar antes de suscribir para que una baja concurrente
                    // pueda identificar la conexión desde el primer momento.
                    _registry.Register(Context.ConnectionId, usuarioId, restauranteId, grupos);
                    foreach (var grupo in grupos)
                        await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
                }
            }
        }

        await base.OnConnectedAsync();
    }
}
