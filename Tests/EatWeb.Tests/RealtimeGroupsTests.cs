using EatWeb.Realtime;
using Xunit;

namespace EatWeb.Tests;

public class RealtimeGroupsTests
{
    [Fact]
    public void Garzon_NoComparteGrupoEntreRestaurantes()
    {
        const string usuarioId = "garzon-123";

        var primerLocal = RealtimeGroups.Garzon(10, usuarioId);
        var segundoLocal = RealtimeGroups.Garzon(20, usuarioId);

        Assert.NotEqual(primerLocal, segundoLocal);
        Assert.Equal(primerLocal, RealtimeGroups.Garzon(10, usuarioId));
    }
}
