using EatWeb.Realtime;
using Xunit;

namespace EatWeb.Tests;

public class RealtimeConnectionRegistryTests
{
    [Fact]
    public void RevokeTarget_NoAfectaLosGruposDeOtroRestaurante()
    {
        var registry = new RealtimeConnectionRegistry();
        registry.Register("conn-1", "usuario-A", 10,
            new[] { RealtimeGroups.Restaurante(10), RealtimeGroups.Garzon(10, "usuario-A") });
        registry.Register("conn-1", "usuario-A", 20,
            new[] { RealtimeGroups.Restaurante(20), RealtimeGroups.Garzon(20, "usuario-A") });
        registry.Register("conn-2", "usuario-B", 10,
            new[] { RealtimeGroups.Restaurante(10) });

        var revocados = registry.Find("usuario-A", 10);
        var revocado = Assert.Single(revocados);
        Assert.Equal("conn-1", revocado.ConnectionId);
        Assert.Contains(RealtimeGroups.Garzon(10, "usuario-A"), revocado.Groups);

        registry.RemoveRestaurant("conn-1", 10);

        Assert.Empty(registry.Find("usuario-A", 10));
        Assert.Single(registry.Find("usuario-A", 20));
        Assert.Single(registry.Find("usuario-B", 10));
    }

    [Fact]
    public void Desconexion_LimpiaRegistroCompleto()
    {
        var registry = new RealtimeConnectionRegistry();
        registry.Register("conn-1", "usuario-A", 10,
            new[] { RealtimeGroups.Restaurante(10) });

        registry.Unregister("conn-1");

        Assert.Empty(registry.Find("usuario-A", 10));
    }
}
