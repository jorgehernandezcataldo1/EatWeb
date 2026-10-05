using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class ResultadoSolicitudMesa
{
    public bool Ok { get; set; }
    public int? SolicitudId { get; set; }
    public bool YaExistia { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class SolicitudMesaService
{
    private readonly ApplicationDbContext _context;

    public SolicitudMesaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ResultadoSolicitudMesa> CrearAsync(
        int sesionId,
        int comensalId,
        string tipo,
        string? mensaje)
    {
        if (!TipoSolicitudMesa.Todos().Contains(tipo))
            return Error("Tipo de solicitud inválido.");

        var mensajeLimpio = string.Join(' ', (mensaje ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (mensajeLimpio.Length > 200)
            return Error("El mensaje no puede superar 200 caracteres.");

        if (tipo == TipoSolicitudMesa.SolicitarAlgo && mensajeLimpio.Length < 2)
            return Error("Escribe qué necesitas.");

        var sesion = await _context.MesaSesiones
            .Include(s => s.Comensales)
            .FirstOrDefaultAsync(s =>
                s.Id == sesionId &&
                s.FechaCierre == null);

        if (sesion == null || sesion.Comensales.All(c => c.Id != comensalId))
            return Error("La mesa ya no está disponible.");

        if (sesion.Estado == EstadoSesion.Pagando)
            return Error("La cuenta ya se está procesando.");

        var existente = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.MesaSesionId == sesionId &&
                s.ComensalId == comensalId &&
                s.Tipo == tipo &&
                s.Estado == EstadoSolicitudMesa.Pendiente)
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        if (existente != null)
        {
            return new ResultadoSolicitudMesa
            {
                Ok = true,
                SolicitudId = existente.Id,
                YaExistia = true
            };
        }

        if (tipo == TipoSolicitudMesa.PedirCuenta &&
            sesion.CuentaSolicitadaEn.HasValue)
        {
            var solicitudCuenta = await _context.SolicitudesMesa
                .AsNoTracking()
                .Where(s =>
                    s.MesaSesionId == sesionId &&
                    s.Tipo == TipoSolicitudMesa.PedirCuenta &&
                    s.Estado != EstadoSolicitudMesa.Cancelada)
                .OrderByDescending(s => s.FechaCreacion)
                .FirstOrDefaultAsync();

            if (solicitudCuenta != null)
            {
                return new ResultadoSolicitudMesa
                {
                    Ok = true,
                    SolicitudId = solicitudCuenta.Id,
                    YaExistia = true
                };
            }
        }

        var solicitud = new SolicitudMesa
        {
            MesaSesionId = sesionId,
            ComensalId = comensalId,
            Tipo = tipo,
            Mensaje = string.IsNullOrWhiteSpace(mensajeLimpio) ? null : mensajeLimpio,
            Estado = EstadoSolicitudMesa.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };

        _context.SolicitudesMesa.Add(solicitud);

        if (tipo == TipoSolicitudMesa.PedirCuenta)
        {
            sesion.CuentaSolicitadaEn ??= solicitud.FechaCreacion;
            sesion.Estado = EstadoSesion.CuentaSolicitada;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();

            var concurrente = await _context.SolicitudesMesa
                .AsNoTracking()
                .Where(s =>
                    s.MesaSesionId == sesionId &&
                    s.ComensalId == comensalId &&
                    s.Tipo == tipo &&
                    s.Estado == EstadoSolicitudMesa.Pendiente)
                .OrderByDescending(s => s.FechaCreacion)
                .FirstOrDefaultAsync();

            if (concurrente != null)
            {
                return new ResultadoSolicitudMesa
                {
                    Ok = true,
                    SolicitudId = concurrente.Id,
                    YaExistia = true
                };
            }

            throw;
        }

        return new ResultadoSolicitudMesa
        {
            Ok = true,
            SolicitudId = solicitud.Id
        };
    }

    public async Task<ResultadoSolicitudMesa> CancelarAsync(
        int solicitudId,
        int comensalId)
    {
        var solicitud = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.Id == solicitudId &&
                s.ComensalId == comensalId)
            .Select(s => new
            {
                s.Id,
                s.MesaSesionId,
                s.Tipo,
                s.Estado
            })
            .FirstOrDefaultAsync();

        if (solicitud == null)
            return Error("Solicitud no encontrada.");

        if (solicitud.Estado != EstadoSolicitudMesa.Pendiente)
            return Error("Solo puedes cancelar solicitudes pendientes.");

        var ahora = DateTime.UtcNow;
        var actualizadas = await _context.SolicitudesMesa
            .Where(s =>
                s.Id == solicitudId &&
                s.ComensalId == comensalId &&
                s.Estado == EstadoSolicitudMesa.Pendiente)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Estado, EstadoSolicitudMesa.Cancelada)
                .SetProperty(s => s.FechaResolucion, ahora));

        if (actualizadas == 0)
            return Error("La solicitud ya fue atendida o cancelada.");

        if (solicitud.Tipo == TipoSolicitudMesa.PedirCuenta)
        {
            var otraCuentaActiva = await _context.SolicitudesMesa
                .AsNoTracking()
                .AnyAsync(s =>
                    s.MesaSesionId == solicitud.MesaSesionId &&
                    s.Id != solicitud.Id &&
                    s.Tipo == TipoSolicitudMesa.PedirCuenta &&
                    s.Estado != EstadoSolicitudMesa.Cancelada);

            if (!otraCuentaActiva)
            {
                var sesion = await _context.MesaSesiones
                    .FirstOrDefaultAsync(s =>
                        s.Id == solicitud.MesaSesionId &&
                        s.FechaCierre == null);

                if (sesion != null && sesion.Estado == EstadoSesion.CuentaSolicitada)
                {
                    sesion.CuentaSolicitadaEn = null;
                    sesion.Estado = EstadoSesion.Abierta;
                    await _context.SaveChangesAsync();
                }
            }
        }

        return new ResultadoSolicitudMesa
        {
            Ok = true,
            SolicitudId = solicitud.Id
        };
    }

    public async Task<ResultadoSolicitudMesa> AtenderAsync(
        int solicitudId,
        int restauranteId,
        string usuarioId,
        bool esAdmin)
    {
        var solicitud = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s => s.Id == solicitudId)
            .Select(s => new
            {
                s.Id,
                s.Estado,
                RestauranteId = s.MesaSesion!.Mesa!.RestauranteId,
                SesionGarzonId = s.MesaSesion.GarzonId,
                MesaGarzonId = s.MesaSesion.Mesa.GarzonId
            })
            .FirstOrDefaultAsync();

        if (solicitud == null || solicitud.RestauranteId != restauranteId)
            return Error("Solicitud no encontrada.");

        if (!esAdmin &&
            solicitud.SesionGarzonId != usuarioId &&
            solicitud.MesaGarzonId != usuarioId)
            return Error("No tienes acceso a esta mesa.");

        if (solicitud.Estado != EstadoSolicitudMesa.Pendiente)
        {
            return new ResultadoSolicitudMesa
            {
                Ok = solicitud.Estado == EstadoSolicitudMesa.Atendida,
                SolicitudId = solicitud.Id,
                YaExistia = true,
                Errores = solicitud.Estado == EstadoSolicitudMesa.Cancelada
                    ? new List<string> { "La solicitud fue cancelada por el cliente." }
                    : new List<string>()
            };
        }

        var ahora = DateTime.UtcNow;
        var actualizadas = await _context.SolicitudesMesa
            .Where(s =>
                s.Id == solicitudId &&
                s.Estado == EstadoSolicitudMesa.Pendiente)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Estado, EstadoSolicitudMesa.Atendida)
                .SetProperty(s => s.FechaResolucion, ahora)
                .SetProperty(s => s.AtendidaPorId, usuarioId));

        if (actualizadas == 0)
        {
            var estadoActual = await _context.SolicitudesMesa
                .AsNoTracking()
                .Where(s => s.Id == solicitudId)
                .Select(s => s.Estado)
                .FirstOrDefaultAsync();

            return estadoActual == EstadoSolicitudMesa.Atendida
                ? new ResultadoSolicitudMesa
                {
                    Ok = true,
                    SolicitudId = solicitudId,
                    YaExistia = true
                }
                : Error("La solicitud fue cancelada por el cliente.");
        }

        return new ResultadoSolicitudMesa
        {
            Ok = true,
            SolicitudId = solicitud.Id
        };
    }

    private static ResultadoSolicitudMesa Error(string mensaje) =>
        new() { Errores = { mensaje } };
}
