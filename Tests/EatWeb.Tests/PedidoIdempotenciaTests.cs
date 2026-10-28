using EatWeb.Models;
using EatWeb.Services;
using Xunit;

namespace EatWeb.Tests;

public class PedidoIdempotenciaTests
{
    private static CarritoSesion Carrito(int cantidad = 2, string nota = "Sin cebolla")
    {
        return new CarritoSesion
        {
            ComensalId = 5,
            Lineas = new List<LineaCarrito>
            {
                new()
                {
                    LineaId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    ProductoId = 42,
                    Cantidad = cantidad,
                    Observacion = nota,
                    IngredientesQuitar = new List<int> { 3, 1 },
                    IngredientesAgregar = new List<int> { 9, 7 }
                }
            }
        };
    }

    [Fact]
    public void ReintentoConMismoContenido_RecuperaPedidoExistente()
    {
        var carrito = Carrito();
        var existente = new Pedido
        {
            Id = 35,
            RequestFingerprint = PedidoService.CalcularFingerprint(carrito, "Mesa 5")
        };

        var resultado = PedidoService.ResolverReintento(existente, carrito, "Mesa 5");

        Assert.True(resultado.Ok);
        Assert.Equal(35, resultado.PedidoId);
    }

    [Fact]
    public void ReintentoConCantidadDistinta_NoReutilizaPedido()
    {
        var existente = new Pedido
        {
            Id = 35,
            RequestFingerprint = PedidoService.CalcularFingerprint(Carrito(), "Mesa 5")
        };

        var resultado = PedidoService.ResolverReintento(
            existente, Carrito(cantidad: 3), "Mesa 5");

        Assert.False(resultado.Ok);
        Assert.Null(resultado.PedidoId);
        Assert.NotEmpty(resultado.Errores);
    }

    [Fact]
    public void ReintentoConNotaDistinta_NoReutilizaPedido()
    {
        var existente = new Pedido
        {
            Id = 35,
            RequestFingerprint = PedidoService.CalcularFingerprint(Carrito(), "Mesa 5")
        };

        var resultado = PedidoService.ResolverReintento(
            existente, Carrito(), "Mesa 6");

        Assert.False(resultado.Ok);
        Assert.NotEmpty(resultado.Errores);
    }

    [Fact]
    public void Fingerprint_OrdenDeIngredientesNoAlteraElResultado()
    {
        var primero = Carrito();
        var segundo = Carrito();
        segundo.Lineas[0].IngredientesQuitar.Reverse();
        segundo.Lineas[0].IngredientesAgregar.Reverse();

        Assert.Equal(
            PedidoService.CalcularFingerprint(primero, "Mesa 5"),
            PedidoService.CalcularFingerprint(segundo, "Mesa 5"));
    }
}
