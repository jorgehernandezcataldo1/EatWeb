using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CategoriasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RestauranteContextService _restauranteContext;

    public CategoriasController(
        ApplicationDbContext context,
        RestauranteContextService restauranteContext)
    {
        _context = context;
        _restauranteContext = restauranteContext;
    }

    private async Task<int?> GetRestauranteIdAsync()
    {
        var restaurante = await _restauranteContext.ObtenerActualAsync();
        return restaurante?.Id;
    }

    public async Task<IActionResult> Index()
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return RedirectToAction("Index", "Admin");

        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == restauranteId.Value)
            .OrderBy(c => c.Orden)
            .Select(c => new CategoriaViewModel
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Orden = c.Orden,
                Activa = c.Activa
            })
            .ToListAsync();

        return View(categorias);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return RedirectToAction("Index", "Admin");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CategoriaViewModel modelo)
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        if (!ModelState.IsValid)
            return View(modelo);

        var existe = await _context.Categorias
            .AnyAsync(c =>
                c.RestauranteId == restauranteId.Value &&
                c.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(
                nameof(modelo.Nombre),
                "Ya existe una categoría con este nombre");

            return View(modelo);
        }

        var categoria = new Categoria
        {
            RestauranteId = restauranteId.Value,
            Nombre = modelo.Nombre,
            Orden = modelo.Orden,
            Activa = modelo.Activa
        };

        _context.Categorias.Add(categoria);

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Categoría creada exitosamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == restauranteId.Value);

        if (categoria == null)
            return NotFound();

        return View(new CategoriaViewModel
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Orden = categoria.Orden,
            Activa = categoria.Activa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        CategoriaViewModel modelo)
    {
        if (id != modelo.Id || !ModelState.IsValid)
            return View(modelo);

        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == restauranteId.Value);

        if (categoria == null)
            return NotFound();

        var existe = await _context.Categorias
            .AnyAsync(c =>
                c.RestauranteId == restauranteId.Value &&
                c.Nombre == modelo.Nombre &&
                c.Id != id);

        if (existe)
        {
            ModelState.AddModelError(
                nameof(modelo.Nombre),
                "Ya existe otra categoría con este nombre");

            return View(modelo);
        }

        categoria.Nombre = modelo.Nombre;
        categoria.Orden = modelo.Orden;
        categoria.Activa = modelo.Activa;

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Categoría actualizada exitosamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.RestauranteId == restauranteId.Value);

        if (categoria != null)
        {
            categoria.Activa = !categoria.Activa;

            await _context.SaveChangesAsync();

            TempData["Ok"] =
                $"Categoría {(categoria.Activa ? "activada" : "desactivada")}";
        }

        return RedirectToAction(nameof(Index));
    }
}
