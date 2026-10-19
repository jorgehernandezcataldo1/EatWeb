using Microsoft.AspNetCore.SignalR;

namespace EatWeb.Realtime;

public sealed record OperacionRealtimeEvent(
    string Tipo,
    int RestauranteId,
    int? SesionId,
    DateTime FechaUtc);

public class OperacionRealtimeService
{
    private readonly IHubContext<ComiditaHub> _hub;
    private readonly ILogger<OperacionRealtimeService> _logger;

    public OperacionRealtimeService(
        IHubContext<ComiditaHub> hub,
        ILogger<OperacionRealtimeService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublicarAsync(
        int restauranteId,
        string tipo,
        int? sesionId = null,
        string? garzonId = null,
        IEnumerable<string>? estaciones = null)
    {
        try
        {
            var evento = new OperacionRealtimeEvent(
                tipo,
                restauranteId,
                sesionId,
                DateTime.UtcNow);

            await _hub.Clients
                .Group(RealtimeGroups.Restaurante(restauranteId))
                .SendAsync("OperacionActualizada", evento);

            if (!string.IsNullOrWhiteSpace(garzonId))
            {
                await _hub.Clients
                    .Group(RealtimeGroups.Garzon(garzonId))
                    .SendAsync("GarzonActualizado", evento);
            }

            if (sesionId.HasValue)
            {
                await _hub.Clients
                    .Group(RealtimeGroups.Sesion(sesionId.Value))
                    .SendAsync("MesaActualizada", evento);
            }

            if (estaciones != null)
            {
                foreach (var estacion in estaciones
                             .Where(e => !string.IsNullOrWhiteSpace(e))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _hub.Clients
                        .Group(RealtimeGroups.Estacion(restauranteId, estacion))
                        .SendAsync("EstacionActualizada", evento);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo publicar evento realtime {Tipo} del restaurante {RestauranteId}",
                tipo,
                restauranteId);
        }
    }
}
