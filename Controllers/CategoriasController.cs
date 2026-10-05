using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CategoriasController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    public CategoriasController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == RestauranteId)
            .Include(c => c.Estacion)
            .OrderBy(c => c.Orden)
            .Select(c => new CategoriaViewModel
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Orden = c.Orden,
                Activa = c.Activa,
                EstacionId = c.EstacionId,
                EstacionNombre = c.Estacion != null ? c.Estacion.Nombre : null
            })
            .ToListAsync();

        return View(categorias);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        return View(await PrepararEstacionesAsync(new CategoriaViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CategoriaViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(await PrepararEstacionesAsync(modelo));

        var existe = await _context.Categorias
            .AnyAsync(c =>
                c.RestauranteId == RestauranteId &&
                c.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(
                nameof(modelo.Nombre),
                "Ya existe una categoría con este nombre");

            return View(await PrepararEstacionesAsync(modelo));
        }

        if (!await EstacionValidaAsync(modelo.EstacionId)) return BadRequest();

        var categoria = new Categoria
        {
            RestauranteId = RestauranteId,
            Nombre = modelo.Nombre,
            Orden = modelo.Orden,
            Activa = modelo.Activa,
            EstacionId = modelo.EstacionId
        };

        _context.Categorias.Add(categoria);

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Categoría creada exitosamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == RestauranteId);

        if (categoria == null)
            return NotFound();

        return View(new CategoriaViewModel
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Orden = categoria.Orden,
            Activa = categoria.Activa,
            EstacionId = categoria.EstacionId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        CategoriaViewModel modelo)
    {
        if (id != modelo.Id || !ModelState.IsValid)
            return View(await PrepararEstacionesAsync(modelo));

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == RestauranteId);

        if (categoria == null)
            return NotFound();

        var existe = await _context.Categorias
            .AnyAsync(c =>
                c.RestauranteId == RestauranteId &&
                c.Nombre == modelo.Nombre &&
                c.Id != id);

        if (existe)
        {
            ModelState.AddModelError(
                nameof(modelo.Nombre),
                "Ya existe otra categoría con este nombre");

            return View(await PrepararEstacionesAsync(modelo));
        }

        if (!await EstacionValidaAsync(modelo.EstacionId)) return BadRequest();

        categoria.Nombre = modelo.Nombre;
        categoria.Orden = modelo.Orden;
        categoria.Activa = modelo.Activa;
        categoria.EstacionId = modelo.EstacionId;

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Categoría actualizada exitosamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == RestauranteId);

        if (categoria != null)
        {
            categoria.Activa = !categoria.Activa;

            await _context.SaveChangesAsync();

            TempData["Ok"] =
                $"Categoría {(categoria.Activa ? "activada" : "desactivada")}";
        }

        return RedirectToAction(nameof(Index));
    }
    private async Task<CategoriaViewModel> PrepararEstacionesAsync(CategoriaViewModel modelo)
    {
        modelo.Estaciones = await _context.Estaciones
            .Where(e => e.RestauranteId == RestauranteId && e.Activa)
            .OrderBy(e => e.Orden)
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(e.Nombre, e.Id.ToString()))
            .ToListAsync();
        return modelo;
    }

    private async Task<bool> EstacionValidaAsync(int? estacionId) =>
        !estacionId.HasValue || await _context.Estaciones.AnyAsync(e => e.Id == estacionId && e.RestauranteId == RestauranteId && e.Activa);

}
