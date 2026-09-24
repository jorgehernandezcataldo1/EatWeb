using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class ProductosController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductosController(ApplicationDbContext context)
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
        var productos = await _context.Productos
            .AsNoTracking()
            .Where(p => p.RestauranteId == restauranteId)
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoViewModel
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                ImagenUrl = p.ImagenUrl,
                CategoriaId = p.CategoriaId,
                Activo = p.Activo,
                Disponible = p.Disponible
            })
            .ToListAsync();

        return View(productos);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var restauranteId = GetRestauranteId();
        var categorias = await _context.Categorias
            .Where(c => c.RestauranteId == restauranteId && c.Activa)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        ViewBag.Categorias = new SelectList(categorias, "Id", "Nombre");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ProductoViewModel modelo)
    {
        var restauranteId = GetRestauranteId();

        if (!ModelState.IsValid)
        {
            var categorias = await _context.Categorias
                .Where(c => c.RestauranteId == restauranteId && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
            ViewBag.Categorias = new SelectList(categorias, "Id", "Nombre");
            return View(modelo);
        }

        // Validar que la categoría pertenece al restaurante
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == modelo.CategoriaId && c.RestauranteId == restauranteId);

        if (categoria == null)
            return BadRequest("Categoría inválida");

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == restauranteId && p.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe un producto con este nombre");
            var catsError = await _context.Categorias
                .Where(c => c.RestauranteId == restauranteId && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
            ViewBag.Categorias = new SelectList(catsError, "Id", "Nombre");
            return View(modelo);
        }

        var producto = new Producto
        {
            RestauranteId = restauranteId,
            Nombre = modelo.Nombre,
            Descripcion = modelo.Descripcion,
            Precio = modelo.Precio,
            ImagenUrl = modelo.ImagenUrl,
            CategoriaId = modelo.CategoriaId,
            Activo = modelo.Activo,
            Disponible = modelo.Disponible
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["Ok"] = "Producto creado exitosamente";
        return RedirectToAction(nameof(Ingredientes), new { productoId = producto.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        var categorias = await _context.Categorias
            .Where(c => c.RestauranteId == restauranteId && c.Activa)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        ViewBag.Categorias = new SelectList(categorias, "Id", "Nombre", producto.CategoriaId);

        var modelo = new ProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            ImagenUrl = producto.ImagenUrl,
            CategoriaId = producto.CategoriaId,
            Activo = producto.Activo,
            Disponible = producto.Disponible
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ProductoViewModel modelo)
    {
        if (id != modelo.Id || !ModelState.IsValid)
        {
            var categoriasError = await _context.Categorias
                .Where(c => c.RestauranteId == GetRestauranteId() && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
            ViewBag.Categorias = new SelectList(categoriasError, "Id", "Nombre");
            return View(modelo);
        }

        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == modelo.CategoriaId && c.RestauranteId == restauranteId);

        if (categoria == null)
            return BadRequest("Categoría inválida");

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == restauranteId && p.Nombre == modelo.Nombre && p.Id != id);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otro producto con este nombre");
            var catsError = await _context.Categorias
                .Where(c => c.RestauranteId == restauranteId && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
            ViewBag.Categorias = new SelectList(catsError, "Id", "Nombre");
            return View(modelo);
        }

        producto.Nombre = modelo.Nombre;
        producto.Descripcion = modelo.Descripcion;
        producto.Precio = modelo.Precio;
        producto.ImagenUrl = modelo.ImagenUrl;
        producto.CategoriaId = modelo.CategoriaId;
        producto.Activo = modelo.Activo;
        producto.Disponible = modelo.Disponible;

        await _context.SaveChangesAsync();

        TempData["Ok"] = "Producto actualizado exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Ingredientes(int productoId)
    {
        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == productoId && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        var ingredientesAsignados = await _context.ProductoIngredientes
            .Where(pi => pi.ProductoId == productoId)
            .Select(pi => new ProductoIngredienteViewModel
            {
                ProductoId = pi.ProductoId,
                IngredienteId = pi.IngredienteId,
                Tipo = pi.Tipo,
                PrecioExtra = pi.PrecioExtra,
                NombreIngrediente = pi.Ingrediente.Nombre
            })
            .OrderBy(pi => pi.NombreIngrediente)
            .ToListAsync();

        var ingredientesDisponibles = await _context.Ingredientes
            .Where(i => i.RestauranteId == restauranteId && i.Activo)
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        ViewBag.ProductoId = productoId;
        ViewBag.ProductoNombre = producto.Nombre;
        ViewBag.IngredientesDisponibles = ingredientesDisponibles;
        ViewBag.IngredientesAsignados = ingredientesAsignados;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AñadirIngrediente(int productoId, int ingredienteId, string tipo, decimal precioExtra)
    {
        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == productoId && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        var ingrediente = await _context.Ingredientes
            .FirstOrDefaultAsync(i => i.Id == ingredienteId && i.RestauranteId == restauranteId);

        if (ingrediente == null)
            return BadRequest("Ingrediente inválido");

        var existe = await _context.ProductoIngredientes
            .AnyAsync(pi => pi.ProductoId == productoId && pi.IngredienteId == ingredienteId);

        if (existe)
        {
            TempData["Error"] = "Este ingrediente ya está asignado al producto";
            return RedirectToAction(nameof(Ingredientes), new { productoId });
        }

        // Si tipo es Incluido, el precio debe ser 0
        if (tipo == TipoIngrediente.Incluido)
            precioExtra = 0;

        var productoIngrediente = new ProductoIngrediente
        {
            ProductoId = productoId,
            IngredienteId = ingredienteId,
            Tipo = tipo,
            PrecioExtra = precioExtra
        };

        _context.ProductoIngredientes.Add(productoIngrediente);
        await _context.SaveChangesAsync();

        TempData["Ok"] = "Ingrediente asignado exitosamente";
        return RedirectToAction(nameof(Ingredientes), new { productoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverIngrediente(int productoId, int ingredienteId)
    {
        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == productoId && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        var productoIngrediente = await _context.ProductoIngredientes
            .FirstOrDefaultAsync(pi => pi.ProductoId == productoId && pi.IngredienteId == ingredienteId);

        if (productoIngrediente != null)
        {
            _context.ProductoIngredientes.Remove(productoIngrediente);
            await _context.SaveChangesAsync();
            TempData["Ok"] = "Ingrediente removido";
        }

        return RedirectToAction(nameof(Ingredientes), new { productoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var restauranteId = GetRestauranteId();
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == restauranteId);

        if (producto != null)
        {
            producto.Activo = !producto.Activo;
            await _context.SaveChangesAsync();
            TempData["Ok"] = $"Producto {(producto.Activo ? "activado" : "desactivado")}";
        }

        return RedirectToAction(nameof(Index));
    }
}
