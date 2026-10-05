using EatWeb.Data;
using EatWeb.Models;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class EstacionesController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;

    public EstacionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == RestauranteId)
            .OrderBy(e => e.Orden)
            .ThenBy(e => e.Nombre)
            .Select(e => new EstacionViewModel
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Orden = e.Orden,
                Activa = e.Activa
            })
            .ToListAsync();

        return View(estaciones);
    }

    [HttpGet]
    public IActionResult Crear() => View(new EstacionViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(EstacionViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var nombre = modelo.Nombre.Trim();
        if (await _context.Estaciones.AnyAsync(e =>
                e.RestauranteId == RestauranteId && e.Nombre == nombre))
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe una estación con este nombre");
            return View(modelo);
        }

        _context.Estaciones.Add(new Estacion
        {
            RestauranteId = RestauranteId,
            Nombre = nombre,
            Orden = modelo.Orden,
            Activa = modelo.Activa
        });

        await _context.SaveChangesAsync();
        TempData["Ok"] = "Estación creada exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var modelo = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.Id == id && e.RestauranteId == RestauranteId)
            .Select(e => new EstacionViewModel
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Orden = e.Orden,
                Activa = e.Activa
            })
            .FirstOrDefaultAsync();

        return modelo == null ? NotFound() : View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, EstacionViewModel modelo)
    {
        if (id != modelo.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(modelo);

        var estacion = await _context.Estaciones
            .FirstOrDefaultAsync(e => e.Id == id && e.RestauranteId == RestauranteId);

        if (estacion == null)
            return NotFound();

        var nombre = modelo.Nombre.Trim();
        if (await _context.Estaciones.AnyAsync(e =>
                e.RestauranteId == RestauranteId &&
                e.Nombre == nombre &&
                e.Id != id))
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otra estación con este nombre");
            return View(modelo);
        }

        estacion.Nombre = nombre;
        estacion.Orden = modelo.Orden;
        estacion.Activa = modelo.Activa;

        await _context.SaveChangesAsync();
        TempData["Ok"] = "Estación actualizada exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var estacion = await _context.Estaciones
            .FirstOrDefaultAsync(e => e.Id == id && e.RestauranteId == RestauranteId);

        if (estacion == null)
            return NotFound();

        estacion.Activa = !estacion.Activa;
        await _context.SaveChangesAsync();

        TempData["Ok"] = $"Estación {(estacion.Activa ? "activada" : "desactivada")}";
        return RedirectToAction(nameof(Index));
    }
}
