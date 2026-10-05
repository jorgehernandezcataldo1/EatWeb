using EatWeb.Data;
using EatWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class MensajosRapidosService
{
    private readonly ApplicationDbContext _context;

    public MensajosRapidosService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene todos los mensajes rápidos activos de una estación en un restaurante.
    /// </summary>
    public async Task<List<MensajeRapido>> ObtenerPorEstacionAsync(int restauranteId, int estacionId)
    {
        return await _context.MensajesRapidos
            .AsNoTracking()
            .Where(m => m.RestauranteId == restauranteId && m.EstacionId == estacionId && m.Activo)
            .OrderBy(m => m.Orden)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene todos los mensajes rápidos de un restaurante (activos e inactivos).
    /// </summary>
    public async Task<List<MensajeRapido>> ObtenerPorRestauranteAsync(int restauranteId)
    {
        return await _context.MensajesRapidos
            .AsNoTracking()
            .Where(m => m.RestauranteId == restauranteId)
            .OrderBy(m => m.EstacionId)
            .ThenBy(m => m.Orden)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene un mensaje rápido por ID.
    /// </summary>
    public async Task<MensajeRapido?> ObtenerPorIdAsync(int id)
    {
        return await _context.MensajesRapidos.FindAsync(id);
    }

    /// <summary>
    /// Crea un nuevo mensaje rápido.
    /// </summary>
    public async Task<MensajeRapido> CrearAsync(int restauranteId, int estacionId, string texto, int orden = 0)
    {
        var mensaje = new MensajeRapido
        {
            RestauranteId = restauranteId,
            EstacionId = estacionId,
            Texto = texto,
            Orden = orden,
            Activo = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MensajesRapidos.Add(mensaje);
        await _context.SaveChangesAsync();
        return mensaje;
    }

    /// <summary>
    /// Actualiza un mensaje rápido.
    /// </summary>
    public async Task<MensajeRapido?> ActualizarAsync(int id, string? texto = null, int? orden = null, bool? activo = null)
    {
        var mensaje = await _context.MensajesRapidos.FindAsync(id);
        if (mensaje == null)
            return null;

        if (!string.IsNullOrWhiteSpace(texto))
            mensaje.Texto = texto;

        if (orden.HasValue)
            mensaje.Orden = orden.Value;

        if (activo.HasValue)
            mensaje.Activo = activo.Value;

        mensaje.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return mensaje;
    }

    /// <summary>
    /// Elimina un mensaje rápido (soft delete: solo marca como inactivo).
    /// </summary>
    public async Task<bool> EliminarAsync(int id)
    {
        var mensaje = await _context.MensajesRapidos.FindAsync(id);
        if (mensaje == null)
            return false;

        mensaje.Activo = false;
        mensaje.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Elimina permanentemente un mensaje rápido.
    /// </summary>
    public async Task<bool> EliminarPermanentementeAsync(int id)
    {
        var mensaje = await _context.MensajesRapidos.FindAsync(id);
        if (mensaje == null)
            return false;

        _context.MensajesRapidos.Remove(mensaje);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Obtiene las estaciones únicas que tienen mensajes en un restaurante.
    /// </summary>
    public async Task<List<Estacion>> ObtenerEstacionesConMensajesAsync(int restauranteId)
    {
        return await _context.MensajesRapidos
            .AsNoTracking()
            .Where(m => m.RestauranteId == restauranteId && m.Activo)
            .Select(m => m.Estacion)
            .Distinct()
            .OrderBy(e => e.Orden)
            .ToListAsync();
    }
}
