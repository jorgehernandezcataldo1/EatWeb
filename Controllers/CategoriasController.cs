using EatWeb.Data;
using EatWeb.Models;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CategoriasController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriasController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetRestauranteId()
    {
        var claim = User.FindFirst("RestauranteId");
        return int.Parse(claim?.Value ?? "0");
    }

    public async Task<IActionResult> Index()
    {
        var restauranteId = GetRestauranteId();
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == restauranteId)
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
    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CategoriaViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var restauranteId = GetRestauranteId();

        // Validar duplicado
        var existe = await _context.Categorias
            .AnyAsync(c => c.RestauranteId == restauranteId && c.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe una categoría con este nombre");
            return View(modelo);
        }

        var categoria = new Categoria
        {
            RestauranteId = restauranteId,
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
        var restauranteId = GetRestauranteId();
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == id && c.RestauranteId == restauranteId);

        if (categoria == null)
            return NotFound();

        var modelo = new CategoriaViewModel
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Orden = categoria.Orden,
            Activa = categoria.Activa
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, CategoriaViewModel modelo)
    {
        if (id != modelo.Id || !ModelState.IsValid)
            return View(modelo);

        var restauranteId = GetRestauranteId();
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == id && c.RestauranteId == restauranteId);

        if (categoria == null)
            return NotFound();

        // Validar duplicado (excluyendo la actual)
        var existe = await _context.Categorias
            .AnyAsync(c => c.RestauranteId == restauranteId && c.Nombre == modelo.Nombre && c.Id != id);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otra categoría con este nombre");
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
        var restauranteId = GetRestauranteId();
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == id && c.RestauranteId == restauranteId);

        if (categoria != null)
        {
            categoria.Activa = !categoria.Activa;
            await _context.SaveChangesAsync();
            TempData["Ok"] = $"Categoría {(categoria.Activa ? "activada" : "desactivada")}";
        }

        return RedirectToAction(nameof(Index));
    }
}
