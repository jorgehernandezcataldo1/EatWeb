using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;

namespace EatWeb.Tests;

public class PedidoServiceTests
{
    [Fact]
    public void CalcularEstadoPedido_TodosCancelados_RetornaCancelado()
    {
        var detalles = new[]
        {
            Detalle(EstadoDetallePedido.Cancelado),
            Detalle(EstadoDetallePedido.Cancelado)
        };

        Assert.Equal(EstadoPedido.Cancelado, PedidoService.CalcularEstadoPedido(detalles));
    }

    [Fact]
    public void CalcularEstadoPedido_PendienteYCancelado_RetornaPendiente()
    {
        var detalles = new[]
        {
            Detalle(EstadoDetallePedido.Pendiente),
            Detalle(EstadoDetallePedido.Cancelado)
        };

        Assert.Equal(EstadoPedido.Pendiente, PedidoService.CalcularEstadoPedido(detalles));
    }

    [Fact]
    public void CalcularEstadoPedido_AlgunItemEnPreparacion_RetornaEnPreparacion()
    {
        var detalles = new[]
        {
            Detalle(EstadoDetallePedido.EnPreparacion),
            Detalle(EstadoDetallePedido.Pendiente)
        };

        Assert.Equal(EstadoPedido.EnPreparacion, PedidoService.CalcularEstadoPedido(detalles));
    }

    [Fact]
    public void CalcularEstadoPedido_ListoYEntregado_RetornaListo()
    {
        var detalles = new[]
        {
            Detalle(EstadoDetallePedido.Listo),
            Detalle(EstadoDetallePedido.Entregado)
        };

        Assert.Equal(EstadoPedido.Listo, PedidoService.CalcularEstadoPedido(detalles));
    }

    [Fact]
    public void CalcularEstadoPedido_TodosEntregados_RetornaEntregado()
    {
        var detalles = new[]
        {
            Detalle(EstadoDetallePedido.Entregado),
            Detalle(EstadoDetallePedido.Entregado)
        };

        Assert.Equal(EstadoPedido.Entregado, PedidoService.CalcularEstadoPedido(detalles));
    }

    private static DetallePedido Detalle(string estado) => new() { Estado = estado };
}

public class PagoCalculosTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(999, 10, 100)]
    [InlineData(10000, 10, 1000)]
    [InlineData(10000, 12.5, 1250)]
    [InlineData(6595, 15, 989)]
    public void CalcularPropina_RedondeaClp(decimal baseConsumo, decimal porcentaje, decimal esperado)
    {
        Assert.Equal(esperado, PagoCalculos.CalcularPropina(baseConsumo, porcentaje));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void CalcularPropina_PorcentajeFueraDeRango_Lanza(decimal porcentaje)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PagoCalculos.CalcularPropina(10000, porcentaje));
    }

    [Fact]
    public void CalcularPropina_MasDeDosDecimales_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PagoCalculos.CalcularPropina(10000, 10.123m));
    }
}
