using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Garzon)]
public class TablEstacionController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly MensajosRapidosService _mensajesService;
    private readonly HistorialMensajesService _historialService;

    public TablEstacionController(
        ApplicationDbContext context,
        MensajosRapidosService mensajesService,
        HistorialMensajesService historialService)
    {
        _context = context;
        _mensajesService = mensajesService;
        _historialService = historialService;
    }

    // GET: /tabl-estacion/index/{estacionId}
    [HttpGet]
    [Route("/tabl-estacion/index/{estacionId:int}")]
    public async Task<IActionResult> Index(int estacionId)
    {
        // Verificar que la estación pertenece al restaurante
        var estacion = await _context.Estaciones.FindAsync(estacionId);
        if (estacion == null || estacion.RestauranteId != RestauranteId)
            return Forbid();

        ViewData["Estacion"] = estacion;
        ViewData["EstacionId"] = estacionId;

        return View();
    }

    // GET: /api/tabl-estacion/mensajes/{estacionId}
    [HttpGet]
    [Route("/api/tabl-estacion/mensajes/{estacionId:int}")]
    public async Task<IActionResult> ObtenerMensajesSinLeer(int estacionId)
    {
        // Verificar que la estación pertenece al restaurante
        var estacion = await _context.Estaciones.FindAsync(estacionId);
        if (estacion == null || estacion.RestauranteId != RestauranteId)
            return Forbid();

        var mensajes = await _historialService.ObtenerMensajesSinLeerAsync(RestauranteId, estacionId);

        return Ok(mensajes.Select(h => new
        {
            id = h.Id,
            mensajeId = h.MensajeRapidoId,
            texto = h.MensajeRapido.Texto,
            garzón = h.Garzon.NombreCompleto,
            enviadoEn = h.EnviadoEn,
            leido = h.Leido,
            leidoEn = h.LeidoEn
        }).ToList());
    }

    // POST: /api/tabl-estacion/marcar-leido/{historialId}
    [HttpPost]
    [Route("/api/tabl-estacion/marcar-leido/{historialId:int}")]
    public async Task<IActionResult> MarcarComoLeido(int historialId)
    {
        var historial = await _context.HistorialesMensajes.FindAsync(historialId);
        if (historial == null || historial.RestauranteId != RestauranteId)
            return Forbid();

        await _historialService.MarcarComoLeidoAsync(historialId);

        return Ok(new { exito = true });
    }

    // POST: /api/tabl-estacion/marcar-todos-leido/{estacionId}
    [HttpPost]
    [Route("/api/tabl-estacion/marcar-todos-leido/{estacionId:int}")]
    public async Task<IActionResult> MarcarTodosComoLeido(int estacionId)
    {
        // Verificar que la estación pertenece al restaurante
        var estacion = await _context.Estaciones.FindAsync(estacionId);
        if (estacion == null || estacion.RestauranteId != RestauranteId)
            return Forbid();

        int cantidad = await _historialService.MarcarTodosComoLeidoAsync(RestauranteId, estacionId);

        return Ok(new { cantidad, exito = true });
    }

    // GET: /tabl-estacion/historial/{estacionId}
    [HttpGet]
    [Route("/tabl-estacion/historial/{estacionId:int}")]
    public async Task<IActionResult> Historial(int estacionId, int? dias = null)
    {
        // Verificar que la estación pertenece al restaurante
        var estacion = await _context.Estaciones.FindAsync(estacionId);
        if (estacion == null || estacion.RestauranteId != RestauranteId)
            return Forbid();

        var historial = await _historialService.ObtenerHistorialAsync(RestauranteId, estacionId, dias ?? 7);

        ViewData["Estacion"] = estacion;
        ViewData["Dias"] = dias ?? 7;

        return View(historial);
    }
}
