using System.Data;
using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EatWeb.Services;

public record AsignacionPagoInput(int DetallePedidoId, decimal Monto);

public class ResultadoPago
{
    public bool Ok { get; set; }
    public int? PagoId { get; set; }
    public decimal TotalCuenta { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public bool CuentaPagada => SaldoPendiente <= 0;
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

        if (existente != null)
            return existente;

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
        var key = idempotencyKey?.Trim() ?? string.Empty;
        if (key.Length is < 8 or > 64)
        {
            return Error("El identificador idempotente del pago es inválido.");
        }

        // La idempotencia se resuelve antes de exigir que la sesión siga abierta.
        // Así, un reintento del mismo POST después de cerrar la mesa devuelve
        // exactamente el pago ya confirmado en vez de crear uno nuevo o fallar.
        var existente = await BuscarPagoPorIdempotenciaAsync(key);
        if (existente != null)
            return await ResolverPagoExistenteAsync(existente, sesionId);

        var metodoNormalizado = NormalizarMetodo(metodo);
        if (metodoNormalizado == null)
            return Error("Método de pago inválido.");

        if (propina < 0 || decimal.Truncate(propina) != propina)
            return Error("La propina debe ser un monto CLP válido y no negativo.");

        var items = asignaciones.ToList();
        if (items.Count == 0 || items.Any(a => a.Monto <= 0 || decimal.Truncate(a.Monto) != a.Monto))
            return Error("El pago no contiene consumos válidos.");

        if (items.GroupBy(a => a.DetallePedidoId).Any(g => g.Count() > 1))
            return Error("Un consumo no puede aparecer repetido dentro del mismo pago.");

        await using var tx = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            // Segundo chequeo dentro de la transacción para cubrir carreras de reintentos.
            existente = await BuscarPagoPorIdempotenciaAsync(key);
            if (existente != null)
            {
                await tx.RollbackAsync();
                return await ResolverPagoExistenteAsync(existente, sesionId);
            }

            var sesion = await _context.MesaSesiones
                .Include(s => s.Comensales)
                    .ThenInclude(c => c.Pedidos)
                        .ThenInclude(p => p.Detalles)
                .FirstOrDefaultAsync(s => s.Id == sesionId && s.FechaCierre == null);

            if (sesion == null)
            {
                await tx.RollbackAsync();
                return Error("Sesión no encontrada o cerrada.");
            }

            if (comensalId.HasValue &&
                sesion.Comensales.All(c => c.Id != comensalId.Value))
            {
                await tx.RollbackAsync();
                return Error("El comensal indicado no pertenece a esta mesa.");
            }

            var hayPedidosActivos = sesion.Comensales
                .SelectMany(c => c.Pedidos)
                .Any(p =>
                    p.Estado == EstadoPedido.Pendiente ||
                    p.Estado == EstadoPedido.EnPreparacion ||
                    p.Estado == EstadoPedido.Listo);

            if (hayPedidosActivos)
            {
                await tx.RollbackAsync();
                return Error("No se puede cobrar mientras existan pedidos activos.");
            }

            var detallesValidos = sesion.Comensales
                .SelectMany(c => c.Pedidos)
                .Where(p => p.Estado != EstadoPedido.Cancelado)
                .SelectMany(p => p.Detalles)
                .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                .ToDictionary(d => d.Id);

            if (items.Any(a => !detallesValidos.ContainsKey(a.DetallePedidoId)))
            {
                await tx.RollbackAsync();
                return Error("El pago contiene consumos que no pertenecen a esta cuenta.");
            }

            var detalleIds = items.Select(a => a.DetallePedidoId).ToList();
            var yaAsignado = await _context.PagoDetalles
                .Where(pd =>
                    detalleIds.Contains(pd.DetallePedidoId) &&
                    pd.Pago!.Cuenta!.MesaSesionId == sesionId &&
                    pd.Pago.Estado == EstadoPago.Confirmado)
                .GroupBy(pd => pd.DetallePedidoId)
                .Select(g => new
                {
                    DetalleId = g.Key,
                    Monto = g.Sum(x => x.MontoAsignado)
                })
                .ToDictionaryAsync(x => x.DetalleId, x => x.Monto);

            foreach (var item in items)
            {
                var disponible = detallesValidos[item.DetallePedidoId].Subtotal
                    - yaAsignado.GetValueOrDefault(item.DetallePedidoId);

                if (disponible <= 0)
                {
                    await tx.RollbackAsync();
                    return Error($"El consumo {item.DetallePedidoId} ya está pagado.");
                }

                if (item.Monto > disponible)
                {
                    await tx.RollbackAsync();
                    return Error($"El monto asignado al consumo {item.DetallePedidoId} supera su saldo pendiente.");
                }
            }

            var cuenta = await _context.Cuentas
                .FirstOrDefaultAsync(c => c.MesaSesionId == sesionId);

            if (cuenta == null)
            {
                cuenta = new Cuenta
                {
                    MesaSesionId = sesionId,
                    Estado = EstadoCuenta.Abierta,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.Cuentas.Add(cuenta);
            }

            var ahora = DateTime.UtcNow;
            var pago = new Pago
            {
                Cuenta = cuenta,
                ComensalId = comensalId,
                Estado = EstadoPago.Confirmado,
                Metodo = metodoNormalizado,
                IdempotencyKey = key,
                Monto = items.Sum(a => a.Monto),
                Propina = propina,
                FechaCreacion = ahora,
                FechaConfirmacion = ahora,
                Detalles = items.Select(a => new PagoDetalle
                {
                    DetallePedidoId = a.DetallePedidoId,
                    MontoAsignado = a.Monto
                }).ToList()
            };

            _context.Pagos.Add(pago);
            sesion.Estado = EstadoSesion.Pagando;

            await _context.SaveChangesAsync();

            var resultado = new ResultadoPago
            {
                Ok = true,
                PagoId = pago.Id
            };

            await CompletarSaldoAsync(resultado, sesionId);

            if (resultado.CuentaPagada)
            {
                cuenta.Estado = EstadoCuenta.Pagada;
                cuenta.FechaCierre = ahora;
                sesion.Estado = EstadoSesion.Cerrada;
                sesion.FechaCierre = ahora;
            }
            else
            {
                cuenta.Estado = EstadoCuenta.ParcialmentePagada;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return resultado;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.SerializationFailure ||
            ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync();
            _context.ChangeTracker.Clear();

            // Un choque por IdempotencyKey puede significar que otro request idéntico
            // ganó la carrera. En ese caso se devuelve el resultado ya persistido.
            existente = await BuscarPagoPorIdempotenciaAsync(key);
            if (existente != null)
                return await ResolverPagoExistenteAsync(existente, sesionId);

            return Error("La cuenta cambió mientras se registraba el pago. Intenta nuevamente.");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            _context.ChangeTracker.Clear();

            existente = await BuscarPagoPorIdempotenciaAsync(key);
            if (existente != null)
                return await ResolverPagoExistenteAsync(existente, sesionId);

            return Error("No se pudo registrar el pago de forma segura. Intenta nuevamente.");
        }
    }

    private async Task<PagoIdempotenteInfo?> BuscarPagoPorIdempotenciaAsync(string key)
    {
        return await _context.Pagos
            .AsNoTracking()
            .Where(p => p.IdempotencyKey == key)
            .Select(p => new PagoIdempotenteInfo(
                p.Id,
                p.Estado,
                p.Cuenta!.MesaSesionId))
            .FirstOrDefaultAsync();
    }

    private async Task<ResultadoPago> ResolverPagoExistenteAsync(
        PagoIdempotenteInfo existente,
        int sesionId)
    {
        if (existente.MesaSesionId != sesionId)
            return Error("El identificador de pago ya fue utilizado en otra cuenta.");

        var resultado = new ResultadoPago
        {
            Ok = existente.Estado == EstadoPago.Confirmado,
            PagoId = existente.Id
        };

        if (!resultado.Ok)
        {
            resultado.Errores.Add($"El pago existente está en estado {existente.Estado}.");
            return resultado;
        }

        return await CompletarSaldoAsync(resultado, sesionId);
    }

    private async Task<ResultadoPago> CompletarSaldoAsync(
        ResultadoPago resultado,
        int sesionId)
    {
        resultado.TotalCuenta = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesionId == sesionId &&
                d.Pedido.Estado != EstadoPedido.Cancelado &&
                d.Estado != EstadoDetallePedido.Cancelado)
            .SumAsync(d => (decimal?)d.Subtotal) ?? 0;

        resultado.TotalPagado = await _context.PagoDetalles
            .AsNoTracking()
            .Where(pd =>
                pd.Pago!.Cuenta!.MesaSesionId == sesionId &&
                pd.Pago.Estado == EstadoPago.Confirmado)
            .SumAsync(pd => (decimal?)pd.MontoAsignado) ?? 0;

        resultado.SaldoPendiente = Math.Max(
            0,
            resultado.TotalCuenta - resultado.TotalPagado);

        return resultado;
    }

    private static string? NormalizarMetodo(string? metodo)
    {
        return metodo?.Trim().ToLowerInvariant() switch
        {
            "efectivo" => "Efectivo",
            "tarjeta" => "Tarjeta",
            "transferencia" => "Transferencia",
            _ => null
        };
    }

    private static ResultadoPago Error(string mensaje) =>
        new() { Errores = { mensaje } };

    private sealed record PagoIdempotenteInfo(
        int Id,
        string Estado,
        int MesaSesionId);
}
