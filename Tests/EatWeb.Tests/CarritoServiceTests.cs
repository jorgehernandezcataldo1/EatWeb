using EatWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace EatWeb.Tests;

public class CarritoServiceTests
{
    [Fact]
    public void Agregar_IngredientesEnOrdenDistinto_AgrupaLaMismaLinea()
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new SesionPruebaFeature { Session = new SesionPrueba() });
        var service = new CarritoService(new HttpContextAccessor { HttpContext = http });

        service.Agregar(12, 40, 1, new List<int> { 5, 3, 5 },
            new List<int> { 10, 7 }, "sin sal ");
        service.Agregar(12, 40, 2, new List<int> { 3, 5 },
            new List<int> { 7, 10, 10 }, "sin sal");

        var carrito = service.Obtener(12);

        var linea = Assert.Single(carrito.Lineas);
        Assert.Equal(3, linea.Cantidad);
        Assert.Equal(new[] { 3, 5 }, linea.IngredientesQuitar);
        Assert.Equal(new[] { 7, 10 }, linea.IngredientesAgregar);
        Assert.Equal("sin sal", linea.Observacion);
    }

    [Fact]
    public void Obtener_NoExponeCarritoDeOtroComensalEnLaMismaSesion()
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new SesionPruebaFeature { Session = new SesionPrueba() });
        var service = new CarritoService(new HttpContextAccessor { HttpContext = http });

        service.Agregar(12, 40, 1, new List<int>(), new List<int>(), "");

        Assert.Empty(service.Obtener(13).Lineas);
    }

    private sealed class SesionPruebaFeature : ISessionFeature
    {
        public ISession Session { get; set; } = null!;
    }

    private sealed class SesionPrueba : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) =>
            _values.TryGetValue(key, out value!);
    }
}
