using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace EatWeb.Realtime;

/// <summary>
/// Registro local de los grupos de los usuarios autenticados.
/// Permite retirar conexiones que ya estaban abiertas al revocar una membresía.
/// En un despliegue con múltiples instancias debe sustituirse por una solución
/// distribuida (por ejemplo, Azure SignalR o un backplane con control de presencia).
/// </summary>
public sealed class RealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionRegistration> _connections = new();

    public void Register(string connectionId, string userId, int restauranteId, IEnumerable<string> groups)
    {
        _connections.AddOrUpdate(
            connectionId,
            _ => new ConnectionRegistration(userId,
                new ConcurrentDictionary<int, string[]>(new[]
                {
                    new KeyValuePair<int, string[]>(restauranteId, groups.Distinct().ToArray())
                })),
            (_, existing) =>
            {
                if (existing.UserId == userId)
                    existing.GroupsByRestaurante[restauranteId] = groups.Distinct().ToArray();
                return existing;
            });
    }

    public void Unregister(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyList<(string ConnectionId, string[] Groups)> Find(string userId, int restauranteId) =>
        _connections
            .Where(kv => kv.Value.UserId == userId &&
                         kv.Value.GroupsByRestaurante.ContainsKey(restauranteId))
            .Select(kv => (kv.Key, kv.Value.GroupsByRestaurante[restauranteId]))
            .ToList();

    public void RemoveRestaurant(string connectionId, int restauranteId)
    {
        if (_connections.TryGetValue(connectionId, out var registration))
            registration.GroupsByRestaurante.TryRemove(restauranteId, out _);
    }

    private sealed record ConnectionRegistration(
        string UserId,
        ConcurrentDictionary<int, string[]> GroupsByRestaurante);
}

public sealed class RealtimeAccessRevoker
{
    private readonly RealtimeConnectionRegistry _registry;
    private readonly IHubContext<ComiditaHub> _hub;

    public RealtimeAccessRevoker(RealtimeConnectionRegistry registry, IHubContext<ComiditaHub> hub)
    {
        _registry = registry;
        _hub = hub;
    }

    public async Task RevokeRestaurantAsync(string userId, int restauranteId)
    {
        foreach (var registration in _registry.Find(userId, restauranteId))
        {
            foreach (var group in registration.Groups)
                await _hub.Groups.RemoveFromGroupAsync(registration.ConnectionId, group);

            _registry.RemoveRestaurant(registration.ConnectionId, restauranteId);
        }
    }
}
