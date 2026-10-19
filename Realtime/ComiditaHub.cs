using EatWeb.Data;
using EatWeb.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Realtime;

public static class RealtimeGroups
{
    public static string Restaurante(int restauranteId) => $"restaurante:{restauranteId}";
    public static string Garzon(string usuarioId) => $"garzon:{usuarioId}";
    public static string Estacion(int restauranteId, string estacion) =>
        $"estacion:{restauranteId}:{estacion.Trim().ToLowerInvariant()}";
    public static string Sesion(int sesionId) => $"sesion:{sesionId}";
}

public class ComiditaHub : Hub
{
    private const string CookieComensal = "eatweb_comensal";
    private readonly ApplicationDbContext _context;

    public ComiditaHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();

        if (http?.Request.Cookies.TryGetValue(CookieComensal, out var valor) == true &&
            Guid.TryParse(valor, out var token))
        {
            var sesionId = await _context.Comensales
                .AsNoTracking()
                .Where(c =>
                    c.Token == token &&
                    c.MesaSesion!.FechaCierre == null &&
                    c.MesaSesion.Mesa!.Activa &&
                    c.MesaSesion.Mesa.Restaurante!.Activo)
                .Select(c => (int?)c.MesaSesionId)
                .FirstOrDefaultAsync();

            if (sesionId.HasValue)
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Sesion(sesionId.Value));
        }

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var usuarioId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(usuarioId))
            {
                var restaurantes = new List<int>();

                if (Context.User.IsInRole(Roles.AdminCadena))
                {
                    restaurantes = await _context.Restaurantes
                        .AsNoTracking()
                        .Where(r =>
                            r.CadenaId != null &&
                            _context.CadenasMiembros.Any(cm =>
                                cm.UsuarioId == usuarioId &&
                                cm.CadenaId == r.CadenaId))
                        .Select(r => r.Id)
                        .ToListAsync();
                }
                else
                {
                    restaurantes = await _context.RestaurantesMiembros
                        .AsNoTracking()
                        .Where(rm => rm.UsuarioId == usuarioId)
                        .Select(rm => rm.RestauranteId)
                        .Distinct()
                        .ToListAsync();
                }

                foreach (var restauranteId in restaurantes)
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        RealtimeGroups.Restaurante(restauranteId));

                if (Context.User.IsInRole(Roles.Garzon))
                    await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Garzon(usuarioId));

                if (Context.User.IsInRole(Roles.Cocina))
                {
                    foreach (var restauranteId in restaurantes)
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId,
                            RealtimeGroups.Estacion(restauranteId, "Cocina"));
                }

                if (Context.User.IsInRole(Roles.Bar))
                {
                    foreach (var restauranteId in restaurantes)
                        await Groups.AddToGroupAsync(
                            Context.ConnectionId,
                            RealtimeGroups.Estacion(restauranteId, "Bar"));
                }
            }
        }

        await base.OnConnectedAsync();
    }
}
