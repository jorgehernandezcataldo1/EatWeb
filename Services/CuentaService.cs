using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public record AsignacionPagoInput(int DetallePedidoId, decimal Monto);

public class ResultadoPago
{
    public bool Ok { get; set; }
    public int? PagoId { get; set; }
    public decimal TotalCuenta { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public bool CuentaPagada => SaldoPendiente == 0;
    public List<string> Errores { get; set; } = new();
}

public class CuentaService
{
    private readonly ApplicationDbContext _context;

    public CuentaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Cuenta> ObtenerOCrearCuentaAsync(int sesionId)
    {
        var existente = await _context.Cuentas
            .FirstOrDefaultAsync(c => c.MesaSesionId == sesionId);

        if (existente != null) return existente;

        var cuenta = new Cuenta
        {
            MesaSesionId = sesionId,
            Estado = EstadoCuenta.Abierta,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Cuentas.Add(cuenta);
        try
        {
            await _context.SaveChangesAsync();
            return cuenta;
        }
        catch (DbUpdateException)
        {
            _context.Entry(cuenta).State = EntityState.Detached;
            return await _context.Cuentas.SingleAsync(c => c.MesaSesionId == sesionId);
        }
    }

    public async Task<ResultadoPago> RegistrarPagoConfirmadoAsync(
        int sesionId,
        int? comensalId,
        IEnumerable<AsignacionPagoInput> asignaciones,
        string metodo,
        decimal propina = 0,
        string? idempotencyKey = null)
    {
        var resultado = new ResultadoPago();
        var items = asignaciones.Where(a => a.Monto > 0).ToList();

        if (items.Count == 0)
        {
            resultado.Errores.Add("El pago no contiene consumos.");
            return resultado;
        }

        if (propina < 0)
        {
            resultado.Errores.Add("La propina no puede ser negativa.");
            return resultado;
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        var sesion = await _context.MesaSesiones
            .Include(s => s.Comensales)
            .ThenInclude(c => c.Pedidos)
            .ThenInclude(p => p.Detalles)
            .FirstOrDefaultAsync(s => s.Id == sesionId && s.FechaCierre == null);

        if (sesion == null)
        {
            resultado.Errores.Add("Sesión no encontrada o cerrada.");
            return resultado;
        }

        if (sesion.Comensales.SelectMany(c => c.Pedidos).Any(p =>
            p.Estado == EstadoPedido.Pendiente ||
            p.Estado == EstadoPedido.EnPreparacion ||
            p.Estado == EstadoPedido.Listo))
        {
            resultado.Errores.Add("No se puede cobrar mientras existan pedidos activos.");
            return resultado;
        }

        var detallesValidos = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado != EstadoPedido.Cancelado)
            .SelectMany(p => p.Detalles)
            .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
            .ToDictionary(d => d.Id);

        if (items.Any(a => !detallesValidos.ContainsKey(a.DetallePedidoId)))
        {
            resultado.Errores.Add("El pago contiene consumos que no pertenecen a esta cuenta.");
            return resultado;
        }

        var duplicados = items.GroupBy(a => a.DetallePedidoId).Where(g => g.Count() > 1).ToList();
        if (duplicados.Count > 0)
        {
            resultado.Errores.Add("Un consumo no puede aparecer repetido dentro del mismo pago.");
            return resultado;
        }

        var cuenta = await ObtenerOCrearCuentaAsync(sesionId);
        var detalleIds = items.Select(a => a.DetallePedidoId).ToList();

        var yaAsignado = await _context.PagoDetalles
            .Where(pd => detalleIds.Contains(pd.DetallePedidoId) &&
                         pd.Pago!.Estado == EstadoPago.Confirmado)
            .GroupBy(pd => pd.DetallePedidoId)
            .Select(g => new { DetalleId = g.Key, Monto = g.Sum(x => x.MontoAsignado) })
            .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

        foreach (var item in items)
        {
            var disponible = detallesValidos[item.DetallePedidoId].Subtotal
                - yaAsignado.GetValueOrDefault(item.DetallePedidoId);

            if (item.Monto > disponible)
            {
                resultado.Errores.Add($"El monto asignado al consumo {item.DetallePedidoId} supera su saldo pendiente.");
                return resultado;
            }
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : idempotencyKey.Trim();

        var existente = await _context.Pagos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == key);

        if (existente != null)
        {
            resultado.Ok = existente.Estado == EstadoPago.Confirmado;
            resultado.PagoId = existente.Id;
            return await CompletarSaldoAsync(resultado, sesionId);
        }

        var monto = items.Sum(a => a.Monto);
        var pago = new Pago
        {
            CuentaId = cuenta.Id,
            ComensalId = comensalId,
            Estado = EstadoPago.Confirmado,
            Metodo = metodo,
            IdempotencyKey = key,
            Monto = monto,
            Propina = propina,
            FechaCreacion = DateTime.UtcNow,
            FechaConfirmacion = DateTime.UtcNow,
            Detalles = items.Select(a => new PagoDetalle
            {
                DetallePedidoId = a.DetallePedidoId,
                MontoAsignado = a.Monto
            }).ToList()
        };

        _context.Pagos.Add(pago);
        sesion.Estado = EstadoSesion.Pagando;
        await _context.SaveChangesAsync();

        resultado.Ok = true;
        resultado.PagoId = pago.Id;
        await CompletarSaldoAsync(resultado, sesionId);

        if (resultado.CuentaPagada)
        {
            cuenta.Estado = EstadoCuenta.Pagada;
            cuenta.FechaCierre = DateTime.UtcNow;
            sesion.Estado = EstadoSesion.Cerrada;
            sesion.FechaCierre = DateTime.UtcNow;
        }
        else
        {
            cuenta.Estado = EstadoCuenta.ParcialmentePagada;
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return resultado;
    }

    private async Task<ResultadoPago> CompletarSaldoAsync(ResultadoPago resultado, int sesionId)
    {
        resultado.TotalCuenta = await _context.DetallesPedidos
            .Where(d => d.Pedido!.MesaSesionId == sesionId &&
                        d.Pedido.Estado != EstadoPedido.Cancelado &&
                        d.Estado != EstadoDetallePedido.Cancelado)
            .SumAsync(d => (decimal?)d.Subtotal) ?? 0;

        resultado.TotalPagado = await _context.PagoDetalles
            .Where(pd => pd.Pago!.Cuenta!.MesaSesionId == sesionId &&
                         pd.Pago.Estado == EstadoPago.Confirmado)
            .SumAsync(pd => (decimal?)pd.MontoAsignado) ?? 0;

        resultado.SaldoPendiente = Math.Max(0, resultado.TotalCuenta - resultado.TotalPagado);
        return resultado;
    }
}
