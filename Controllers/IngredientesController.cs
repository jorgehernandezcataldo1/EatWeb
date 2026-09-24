using EatWeb.Data;
using EatWeb.Models;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class IngredientesController : Controller
{
    private readonly ApplicationDbContext _context;

    public IngredientesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetRestauranteId()
    {
        return int.Parse(User.FindFirst("RestauranteId")?.Value ?? "0");
    }

    public async Task<IActionResult> Index()
    {
        var restauranteId = GetRestauranteId();
        var ingredientes = await _context.Ingredientes
            .AsNoTracking()
            .Where(i => i.RestauranteId == restauranteId)
            .OrderBy(i => i.Nombre)
            .Select(i => new IngredienteViewModel
            {
                Id = i.Id,
                Nombre = i.Nombre,
                Activo = i.Activo
            })
            .ToListAsync();

        return View(ingredientes);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(IngredienteViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var restauranteId = GetRestauranteId();

        var existe = await _context.Ingredientes
            .AnyAsync(i => i.RestauranteId == restauranteId && i.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe un ingrediente con este nombre");
            return View(modelo);
        }

        var ingrediente = new Ingrediente
        {
            RestauranteId = restauranteId,
            Nombre = modelo.Nombre,
            Activo = modelo.Activo
        };

        _context.Ingredientes.Add(ingrediente);
        await _context.SaveChangesAsync();

        TempData["Ok"] = "Ingrediente creado exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var restauranteId = GetRestauranteId();
        var ingrediente = await _context.Ingredientes
            .FirstOrDefaultAsync(i => i.Id == id && i.RestauranteId == restauranteId);

        if (ingrediente == null)
            return NotFound();

        var modelo = new IngredienteViewModel
        {
            Id = ingrediente.Id,
            Nombre = ingrediente.Nombre,
            Activo = ingrediente.Activo
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, IngredienteViewModel modelo)
    {
        if (id != modelo.Id || !ModelState.IsValid)
            return View(modelo);

        var restauranteId = GetRestauranteId();
        var ingrediente = await _context.Ingredientes
            .FirstOrDefaultAsync(i => i.Id == id && i.RestauranteId == restauranteId);

        if (ingrediente == null)
            return NotFound();

        var existe = await _context.Ingredientes
            .AnyAsync(i => i.RestauranteId == restauranteId && i.Nombre == modelo.Nombre && i.Id != id);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otro ingrediente con este nombre");
            return View(modelo);
        }

        ingrediente.Nombre = modelo.Nombre;
        ingrediente.Activo = modelo.Activo;

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Ingrediente actualizado exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var restauranteId = GetRestauranteId();
        var ingrediente = await _context.Ingredientes
            .FirstOrDefaultAsync(i => i.Id == id && i.RestauranteId == restauranteId);

        if (ingrediente != null)
        {
            ingrediente.Activo = !ingrediente.Activo;
            await _context.SaveChangesAsync();
            TempData["Ok"] = $"Ingrediente {(ingrediente.Activo ? "activado" : "desactivado")}";
        }

        return RedirectToAction(nameof(Index));
    }
}
