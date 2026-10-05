using EatWeb.Data;
using EatWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class HistorialMensajesService
{
    private readonly ApplicationDbContext _context;

    public HistorialMensajesService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Registra el envío de un mensaje rápido en el historial.
    /// </summary>
    public async Task<HistorialMensajeRapido> RegistrarEnvioAsync(int mensajeRapidoId, string garzonId, int restauranteId, int estacionId)
    {
        var historial = new HistorialMensajeRapido
        {
            MensajeRapidoId = mensajeRapidoId,
            RestauranteId = restauranteId,
            EstacionId = estacionId,
            GarzonId = garzonId,
            EnviadoEn = DateTime.UtcNow,
            Leido = false,
            LeidoEn = null
        };

        _context.HistorialesMensajes.Add(historial);
        await _context.SaveChangesAsync();
        return historial;
    }

    /// <summary>
    /// Obtiene el historial de mensajes de una estación sin leer.
    /// </summary>
    public async Task<List<HistorialMensajeRapido>> ObtenerMensajesSinLeerAsync(int restauranteId, int estacionId)
    {
        return await _context.HistorialesMensajes
            .AsNoTracking()
            .Include(h => h.MensajeRapido)
            .Include(h => h.Garzon)
            .Where(h => h.RestauranteId == restauranteId && h.EstacionId == estacionId && !h.Leido)
            .OrderByDescending(h => h.EnviadoEn)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene todo el historial de mensajes de una estación.
    /// </summary>
    public async Task<List<HistorialMensajeRapido>> ObtenerHistorialAsync(int restauranteId, int estacionId, int? ultimosDias = null)
    {
        var query = _context.HistorialesMensajes
            .AsNoTracking()
            .Include(h => h.MensajeRapido)
            .Include(h => h.Garzon)
            .Where(h => h.RestauranteId == restauranteId && h.EstacionId == estacionId);

        if (ultimosDias.HasValue)
        {
            var fechaLimite = DateTime.UtcNow.AddDays(-ultimosDias.Value);
            query = query.Where(h => h.EnviadoEn >= fechaLimite);
        }

        return await query
            .OrderByDescending(h => h.EnviadoEn)
            .ToListAsync();
    }

    /// <summary>
    /// Marca un mensaje del historial como leído.
    /// </summary>
    public async Task<bool> MarcarComoLeidoAsync(int historialId)
    {
        var historial = await _context.HistorialesMensajes.FindAsync(historialId);
        if (historial == null || historial.Leido)
            return false;

        historial.Leido = true;
        historial.LeidoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Marca todos los mensajes sin leer de una estación como leídos.
    /// </summary>
    public async Task<int> MarcarTodosComoLeidoAsync(int restauranteId, int estacionId)
    {
        var mensajes = await _context.HistorialesMensajes
            .Where(h => h.RestauranteId == restauranteId && h.EstacionId == estacionId && !h.Leido)
            .ToListAsync();

        int contador = 0;
        var ahora = DateTime.UtcNow;

        foreach (var msg in mensajes)
        {
            msg.Leido = true;
            msg.LeidoEn = ahora;
            contador++;
        }

        if (contador > 0)
            await _context.SaveChangesAsync();

        return contador;
    }

    /// <summary>
    /// Obtiene el historial completo de un restaurante con filtros opcionales.
    /// </summary>
    public async Task<List<HistorialMensajeRapido>> ObtenerHistorialRestauranteAsync(
        int restauranteId,
        int? estacionId = null,
        string? garzonId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null)
    {
        var query = _context.HistorialesMensajes
            .AsNoTracking()
            .Include(h => h.MensajeRapido)
            .Include(h => h.Garzon)
            .Include(h => h.Estacion)
            .Where(h => h.RestauranteId == restauranteId);

        if (estacionId.HasValue)
            query = query.Where(h => h.EstacionId == estacionId.Value);

        if (!string.IsNullOrWhiteSpace(garzonId))
            query = query.Where(h => h.GarzonId == garzonId);

        if (fechaDesde.HasValue)
            query = query.Where(h => h.EnviadoEn >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(h => h.EnviadoEn <= fechaHasta.Value);

        return await query
            .OrderByDescending(h => h.EnviadoEn)
            .ToListAsync();
    }

    /// <summary>
    /// Estadísticas de mensajes para un restaurante.
    /// </summary>
    public async Task<MensajesEstadisticasDto> ObtenerEstadisticasAsync(int restauranteId, int? ultimosDias = null)
    {
        var query = _context.HistorialesMensajes
            .AsNoTracking()
            .Where(h => h.RestauranteId == restauranteId);

        if (ultimosDias.HasValue)
        {
            var fechaLimite = DateTime.UtcNow.AddDays(-ultimosDias.Value);
            query = query.Where(h => h.EnviadoEn >= fechaLimite);
        }

        var historial = await query.ToListAsync();

        return new MensajesEstadisticasDto
        {
            TotalEnviados = historial.Count,
            TotalLeidos = historial.Count(h => h.Leido),
            TotalSinLeer = historial.Count(h => !h.Leido),
            PorEstacion = historial
                .GroupBy(h => h.EstacionId)
                .ToDictionary(g => g.Key, g => g.Count()),
            PorGarzon = historial
                .GroupBy(h => h.GarzonId)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

public class MensajesEstadisticasDto
{
    public int TotalEnviados { get; set; }
    public int TotalLeidos { get; set; }
    public int TotalSinLeer { get; set; }
    public Dictionary<int, int> PorEstacion { get; set; } = new();
    public Dictionary<string, int> PorGarzon { get; set; } = new();
}
