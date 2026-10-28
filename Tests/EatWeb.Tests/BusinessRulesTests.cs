using Xunit;
using EatWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
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

public class MigrationSnapshotTests
{
    [Fact]
    public void ApplicationDbContext_SnapshotCoincideConModeloActual()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=solo_modelo;Username=test;Password=test")
            .Options;

        using var context = new ApplicationDbContext(options);

        // Compara metadata en memoria; no abre conexión ni modifica Supabase.
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        var actual = context.GetService<IDesignTimeModel>().Model;
        var differ = context.GetService<IMigrationsModelDiffer>();
        var changes = differ.GetDifferences(
            snapshot!.Model.GetRelationalModel(),
            actual.GetRelationalModel());

        Assert.False(changes.Count > 0,
            "Operaciones EF pendientes: " + string.Join("; ", changes.Select(op =>
                op.GetType().Name + " " +
                (op is Microsoft.EntityFrameworkCore.Migrations.Operations.ColumnOperation column
                    ? column.Table + "." + column.Name
                    : op is Microsoft.EntityFrameworkCore.Migrations.Operations.TableOperation table
                        ? table.Name
                        : op is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation index
                            ? index.Table + "." + index.Name
                            : ""))));
    }
}
