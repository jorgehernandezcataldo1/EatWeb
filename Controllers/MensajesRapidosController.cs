using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.AdminRestaurante)]
public class MensajesRapidosController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly MensajosRapidosService _mensajesService;

    public MensajesRapidosController(
        ApplicationDbContext context,
        MensajosRapidosService mensajesService)
    {
        _context = context;
        _mensajesService = mensajesService;
    }

    // GET: /mensajes-rapidos
    [HttpGet]
    public async Task<IActionResult> Index(int? estacionId = null)
    {
        var mensajes = await _mensajesService.ObtenerPorRestauranteAsync(RestauranteId);
        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == RestauranteId)
            .OrderBy(e => e.Orden)
            .ToListAsync();

        ViewData["Estaciones"] = estaciones;
        ViewData["EstacionSeleccionada"] = estacionId;

        if (estacionId.HasValue)
            mensajes = mensajes.Where(m => m.EstacionId == estacionId.Value).ToList();

        return View(mensajes);
    }

    // GET: /mensajes-rapidos/crear
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == RestauranteId)
            .OrderBy(e => e.Orden)
            .ToListAsync();

        ViewData["Estaciones"] = estaciones;
        return View();
    }

    // POST: /mensajes-rapidos/crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(int estacionId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            ModelState.AddModelError(nameof(texto), "El texto del mensaje es requerido.");
            var estaciones = await _context.Estaciones
                .AsNoTracking()
                .Where(e => e.RestauranteId == RestauranteId)
                .OrderBy(e => e.Orden)
                .ToListAsync();
            ViewData["Estaciones"] = estaciones;
            return View();
        }

        // Verificar que la estación pertenece al restaurante
        var estacion = await _context.Estaciones.FindAsync(estacionId);
        if (estacion == null || estacion.RestauranteId != RestauranteId)
            return Forbid();

        // Obtener el máximo orden para la estación
        var maxOrden = await _context.MensajesRapidos
            .Where(m => m.EstacionId == estacionId)
            .MaxAsync(m => (int?)m.Orden) ?? 0;

        await _mensajesService.CrearAsync(RestauranteId, estacionId, texto, maxOrden + 1);

        TempData["Ok"] = "Mensaje rápido creado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /mensajes-rapidos/editar/{id}
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var mensaje = await _mensajesService.ObtenerPorIdAsync(id);
        if (mensaje == null || mensaje.RestauranteId != RestauranteId)
            return NotFound();

        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == RestauranteId)
            .OrderBy(e => e.Orden)
            .ToListAsync();

        ViewData["Estaciones"] = estaciones;
        return View(mensaje);
    }

    // POST: /mensajes-rapidos/editar/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, string texto, int orden, bool activo)
    {
        var mensaje = await _mensajesService.ObtenerPorIdAsync(id);
        if (mensaje == null || mensaje.RestauranteId != RestauranteId)
            return NotFound();

        if (string.IsNullOrWhiteSpace(texto))
        {
            ModelState.AddModelError(nameof(texto), "El texto del mensaje es requerido.");
            var estaciones = await _context.Estaciones
                .AsNoTracking()
                .Where(e => e.RestauranteId == RestauranteId)
                .OrderBy(e => e.Orden)
                .ToListAsync();
            ViewData["Estaciones"] = estaciones;
            return View(mensaje);
        }

        await _mensajesService.ActualizarAsync(id, texto, orden, activo);

        TempData["Ok"] = "Mensaje rápido actualizado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /mensajes-rapidos/eliminar/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var mensaje = await _mensajesService.ObtenerPorIdAsync(id);
        if (mensaje == null || mensaje.RestauranteId != RestauranteId)
            return NotFound();

        await _mensajesService.EliminarAsync(id);

        TempData["Ok"] = "Mensaje rápido eliminado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /mensajes-rapidos/enviar/{id} - Acción para garzones
    [HttpPost]
    [Authorize(Roles = Roles.Garzon)]
    [Route("/api/mensajes-rapidos/enviar/{id:int}")]
    public async Task<IActionResult> EnviarMensaje(int id)
    {
        var mensaje = await _mensajesService.ObtenerPorIdAsync(id);
        if (mensaje == null || mensaje.RestauranteId != RestauranteId)
            return NotFound(new { error = "Mensaje no encontrado." });

        var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioId))
            return Unauthorized();

        var historialService = HttpContext.RequestServices.GetRequiredService<HistorialMensajesService>();
        var historial = await historialService.RegistrarEnvioAsync(
            id,
            usuarioId,
            RestauranteId,
            mensaje.EstacionId);

        return Ok(new
        {
            id = historial.Id,
            mensaje = mensaje.Texto,
            enviadoEn = historial.EnviadoEn,
            estacion = mensaje.Estacion.Nombre
        });
    }
}
