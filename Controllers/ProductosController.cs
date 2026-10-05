using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services.Storage;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class ProductosController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storage;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(
        ApplicationDbContext context,
        IStorageService storage,
        IOptions<StorageOptions> storageOptions,
        ILogger<ProductosController> logger)
    {
        _context = context;
        _storage = storage;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var restauranteId = RestauranteId;
        var datos = await _context.Productos
            .AsNoTracking()
            .Where(p => p.RestauranteId == restauranteId)
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.ImagenKey,
                p.CategoriaId,
                p.Activo,
                p.Disponible
            })
            .ToListAsync();

        var productos = datos.Select(p => new ProductoViewModel
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = p.Precio,
            ImagenUrl = _storage.ObtenerUrlPublica(p.ImagenKey),
            CategoriaId = p.CategoriaId,
            Activo = p.Activo,
            Disponible = p.Disponible
        }).ToList();

        return View(productos);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarCategoriasAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ProductoViewModel modelo)
    {
        var restauranteId = RestauranteId;

        ValidarPrecioClp(modelo);
        var imagen = await ValidarImagenAsync(modelo.Imagen);

        if (!ModelState.IsValid)
        {
            await CargarCategoriasAsync(modelo.CategoriaId);
            return View(modelo);
        }

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == modelo.CategoriaId && c.RestauranteId == restauranteId);

        if (categoria == null)
            return BadRequest("Categoría inválida");

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == restauranteId && p.Nombre == modelo.Nombre);

        if (existe)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe un producto con este nombre");
            await CargarCategoriasAsync(modelo.CategoriaId);
            return View(modelo);
        }

        var producto = new Producto
        {
            RestauranteId = restauranteId,
            Nombre = modelo.Nombre,
            Descripcion = modelo.Descripcion,
            Precio = modelo.Precio,
            CategoriaId = modelo.CategoriaId,
            Activo = modelo.Activo,
            Disponible = modelo.Disponible
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        if (imagen != null && modelo.Imagen != null)
        {
            var key = CrearObjectKey(restauranteId, producto.Id, imagen.Extension);

            try
            {
                await using var stream = modelo.Imagen.OpenReadStream();
                await _storage.SubirAsync(stream, key, imagen.ContentType, HttpContext.RequestAborted);
                producto.ImagenKey = key;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await EliminarStorageSinInterrumpirAsync(key);

                _logger.LogError(ex,
                    "No se pudo subir la imagen del producto {ProductoId} del restaurante {RestauranteId}",
                    producto.Id, restauranteId);

                TempData["Error"] =
                    "El producto fue creado, pero no pudimos subir la foto. Puedes reintentar desde Editar.";

                return RedirectToAction(nameof(Editar), new { id = producto.Id });
            }
        }

        TempData["Ok"] = "Producto creado exitosamente";
        return RedirectToAction(nameof(Ingredientes), new { productoId = producto.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var restauranteId = RestauranteId;
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        await CargarCategoriasAsync(producto.CategoriaId);

        return View(new ProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey),
            CategoriaId = producto.CategoriaId,
            Activo = producto.Activo,
            Disponible = producto.Disponible
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ProductoViewModel modelo)
    {
        if (id != modelo.Id)
            return BadRequest();

        var restauranteId = RestauranteId;
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == restauranteId);

        if (producto == null)
            return NotFound();

        ValidarPrecioClp(modelo);
        var imagen = await ValidarImagenAsync(modelo.Imagen);

        var categoriaValida = await _context.Categorias
            .AnyAsync(c => c.Id == modelo.CategoriaId && c.RestauranteId == restauranteId);

        if (!categoriaValida)
            ModelState.AddModelError(nameof(modelo.CategoriaId), "Categoría inválida");

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == restauranteId &&
                           p.Nombre == modelo.Nombre &&
                           p.Id != id);

        if (existe)
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otro producto con este nombre");

        if (!ModelState.IsValid)
        {
            modelo.ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey);
            await CargarCategoriasAsync(modelo.CategoriaId);
            return View(modelo);
        }

        var imagenAnterior = producto.ImagenKey;
        string? imagenNueva = null;

        producto.Nombre = modelo.Nombre;
        producto.Descripcion = modelo.Descripcion;
        producto.Precio = modelo.Precio;
        producto.CategoriaId = modelo.CategoriaId;
        producto.Activo = modelo.Activo;
        producto.Disponible = modelo.Disponible;

        if (imagen != null && modelo.Imagen != null)
        {
            imagenNueva = CrearObjectKey(restauranteId, producto.Id, imagen.Extension);

            try
            {
                await using var stream = modelo.Imagen.OpenReadStream();
                await _storage.SubirAsync(
                    stream,
                    imagenNueva,
                    imagen.ContentType,
                    HttpContext.RequestAborted);

                producto.ImagenKey = imagenNueva;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo subir la nueva imagen del producto {ProductoId}",
                    producto.Id);

                ModelState.AddModelError(nameof(modelo.Imagen),
                    "No pudimos subir la foto. Revisa la configuración de almacenamiento e intenta nuevamente.");

                modelo.ImagenUrl = _storage.ObtenerUrlPublica(imagenAnterior);
                await CargarCategoriasAsync(modelo.CategoriaId);
                return View(modelo);
            }
        }
        else if (modelo.EliminarImagen)
        {
            producto.ImagenKey = null;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (imagenNueva != null)
                await EliminarStorageSinInterrumpirAsync(imagenNueva);

            throw;
        }

        if (!string.Equals(imagenAnterior, producto.ImagenKey, StringComparison.Ordinal))
            await EliminarStorageSinInterrumpirAsync(imagenAnterior);

        TempData["Ok"] = "Producto actualizado exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Ingredientes(int productoId)
    {
        var restauranteId = RestauranteId;
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
                NombreIngrediente = pi.Ingrediente!.Nombre
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
        var restauranteId = RestauranteId;
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

        if (!TipoIngrediente.Todos().Contains(tipo))
        {
            TempData["Error"] = "Tipo de ingrediente inválido";
            return RedirectToAction(nameof(Ingredientes), new { productoId });
        }

        if (tipo == TipoIngrediente.Incluido)
        {
            precioExtra = 0;
        }
        else if (precioExtra < 0 ||
                 precioExtra > 999999 ||
                 decimal.Truncate(precioExtra) != precioExtra)
        {
            TempData["Error"] = "El precio extra debe ser un monto CLP válido entre $0 y $999.999";
            return RedirectToAction(nameof(Ingredientes), new { productoId });
        }

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
        var restauranteId = RestauranteId;
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
        var restauranteId = RestauranteId;
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

    private async Task CargarCategoriasAsync(int? seleccionada = null)
    {
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == RestauranteId && c.Activa)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        ViewBag.Categorias = new SelectList(categorias, "Id", "Nombre", seleccionada);
    }

    private void ValidarPrecioClp(ProductoViewModel modelo)
    {
        if (modelo.Precio > 0 &&
            decimal.Truncate(modelo.Precio) != modelo.Precio)
        {
            ModelState.AddModelError(
                nameof(modelo.Precio),
                "El precio debe ingresarse en pesos chilenos, sin decimales.");
        }
    }

    private async Task<ImagenValidada?> ValidarImagenAsync(IFormFile? archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return null;

        if (!_storage.EstaConfigurado)
        {
            ModelState.AddModelError(nameof(ProductoViewModel.Imagen),
                "El almacenamiento de imágenes aún no está configurado.");
            return null;
        }

        var maxBytes = _storageOptions.MaxImageBytes > 0
            ? _storageOptions.MaxImageBytes
            : 5 * 1024 * 1024;

        if (archivo.Length > maxBytes)
        {
            ModelState.AddModelError(nameof(ProductoViewModel.Imagen),
                $"La foto no puede superar {Math.Ceiling(maxBytes / 1024d / 1024d):0} MB.");
            return null;
        }

        var cabecera = new byte[12];
        await using var stream = archivo.OpenReadStream();
        var leidos = await stream.ReadAsync(cabecera.AsMemory(0, cabecera.Length));

        if (leidos >= 3 &&
            cabecera[0] == 0xFF &&
            cabecera[1] == 0xD8 &&
            cabecera[2] == 0xFF)
        {
            return new ImagenValidada("jpg", "image/jpeg");
        }

        if (leidos >= 8 &&
            cabecera[0] == 0x89 &&
            cabecera[1] == 0x50 &&
            cabecera[2] == 0x4E &&
            cabecera[3] == 0x47 &&
            cabecera[4] == 0x0D &&
            cabecera[5] == 0x0A &&
            cabecera[6] == 0x1A &&
            cabecera[7] == 0x0A)
        {
            return new ImagenValidada("png", "image/png");
        }

        if (leidos >= 12 &&
            cabecera[0] == (byte)'R' &&
            cabecera[1] == (byte)'I' &&
            cabecera[2] == (byte)'F' &&
            cabecera[3] == (byte)'F' &&
            cabecera[8] == (byte)'W' &&
            cabecera[9] == (byte)'E' &&
            cabecera[10] == (byte)'B' &&
            cabecera[11] == (byte)'P')
        {
            return new ImagenValidada("webp", "image/webp");
        }

        ModelState.AddModelError(nameof(ProductoViewModel.Imagen),
            "Formato no permitido. Usa una imagen JPG, PNG o WebP.");
        return null;
    }

    private static string CrearObjectKey(int restauranteId, int productoId, string extension) =>
        $"restaurantes/{restauranteId}/productos/{productoId}/{Guid.NewGuid():N}.{extension}";

    private async Task EliminarStorageSinInterrumpirAsync(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        try
        {
            if (_storage.EstaConfigurado || Uri.TryCreate(key, UriKind.Absolute, out _))
                await _storage.EliminarAsync(key, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar del storage el objeto {ObjectKey}", key);
        }
    }

    private sealed record ImagenValidada(string Extension, string ContentType);
}