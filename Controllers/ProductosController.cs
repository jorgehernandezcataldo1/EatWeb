using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services.Storage;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var datos = await _context.Productos
            .AsNoTracking()
            .Where(p => p.RestauranteId == RestauranteId)
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.ImagenKey,
                p.CategoriaId,
                CategoriaNombre = p.Categoria!.Nombre,
                EstacionNombre = p.Categoria.Estacion!.Nombre,
                CantidadIngredientes = p.Ingredientes.Count,
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
            CategoriaNombre = p.CategoriaNombre,
            EstacionNombre = p.EstacionNombre,
            CantidadIngredientes = p.CantidadIngredientes,
            Activo = p.Activo,
            Disponible = p.Disponible
        }).ToList();

        return View(productos);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var modelo = new ProductoViewModel();
        await CargarEditorAsync(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ProductoViewModel modelo)
    {
        ValidarPrecioClp(modelo);
        await ValidarCatalogoAsync(modelo);
        var imagen = await ValidarImagenAsync(modelo.Imagen);

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == RestauranteId && p.Nombre == modelo.Nombre);

        if (existe)
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe un producto con este nombre");

        if (!ModelState.IsValid)
        {
            await CargarEditorAsync(modelo);
            return View(modelo);
        }

        var producto = new Producto
        {
            RestauranteId = RestauranteId,
            Nombre = modelo.Nombre.Trim(),
            Descripcion = modelo.Descripcion?.Trim(),
            Precio = modelo.Precio,
            CategoriaId = modelo.CategoriaId,
            Activo = modelo.Activo,
            Disponible = modelo.Disponible,
            FechaCreacion = DateTime.UtcNow
        };

        foreach (var item in modelo.IngredientesSeleccionados.Where(i => i.Seleccionado))
        {
            producto.Ingredientes.Add(new ProductoIngrediente
            {
                IngredienteId = item.IngredienteId,
                Tipo = item.Tipo,
                PrecioExtra = item.Tipo == TipoIngrediente.Incluido ? 0 : item.PrecioExtra
            });
        }

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        if (imagen != null && modelo.Imagen != null)
        {
            var key = CrearObjectKey(RestauranteId, producto.Id, imagen.Extension);

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
                    producto.Id, RestauranteId);

                TempData["Error"] =
                    "El producto fue creado, pero no pudimos subir la foto. Puedes reintentar desde Editar.";

                return RedirectToAction(nameof(Editar), new { id = producto.Id });
            }
        }

        TempData["Ok"] = "Producto creado exitosamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .Include(p => p.Ingredientes)
                .ThenInclude(pi => pi.Ingrediente)
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == RestauranteId);

        if (producto == null)
            return NotFound();

        var modelo = new ProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey),
            CategoriaId = producto.CategoriaId,
            Activo = producto.Activo,
            Disponible = producto.Disponible,
            IngredientesSeleccionados = producto.Ingredientes
                .Select(pi => new ProductoIngredienteInputViewModel
                {
                    IngredienteId = pi.IngredienteId,
                    NombreIngrediente = pi.Ingrediente!.Nombre,
                    Seleccionado = true,
                    Tipo = pi.Tipo,
                    PrecioExtra = pi.PrecioExtra
                })
                .ToList()
        };

        await CargarEditorAsync(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ProductoViewModel modelo)
    {
        if (id != modelo.Id)
            return BadRequest();

        var producto = await _context.Productos
            .Include(p => p.Ingredientes)
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == RestauranteId);

        if (producto == null)
            return NotFound();

        ValidarPrecioClp(modelo);
        await ValidarCatalogoAsync(modelo);
        var imagen = await ValidarImagenAsync(modelo.Imagen);

        var existe = await _context.Productos
            .AnyAsync(p => p.RestauranteId == RestauranteId &&
                           p.Nombre == modelo.Nombre &&
                           p.Id != id);

        if (existe)
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otro producto con este nombre");

        if (!ModelState.IsValid)
        {
            modelo.ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey);
            await CargarEditorAsync(modelo);
            return View(modelo);
        }

        var imagenAnterior = producto.ImagenKey;
        string? imagenNueva = null;

        producto.Nombre = modelo.Nombre.Trim();
        producto.Descripcion = modelo.Descripcion?.Trim();
        producto.Precio = modelo.Precio;
        producto.CategoriaId = modelo.CategoriaId;
        producto.Activo = modelo.Activo;
        producto.Disponible = modelo.Disponible;

        var seleccionados = modelo.IngredientesSeleccionados
            .Where(i => i.Seleccionado)
            .ToDictionary(i => i.IngredienteId);

        foreach (var existente in producto.Ingredientes.ToList())
        {
            if (!seleccionados.TryGetValue(existente.IngredienteId, out var entrada))
            {
                _context.ProductoIngredientes.Remove(existente);
                continue;
            }

            existente.Tipo = entrada.Tipo;
            existente.PrecioExtra = entrada.Tipo == TipoIngrediente.Incluido ? 0 : entrada.PrecioExtra;
            seleccionados.Remove(existente.IngredienteId);
        }

        foreach (var nuevo in seleccionados.Values)
        {
            producto.Ingredientes.Add(new ProductoIngrediente
            {
                ProductoId = producto.Id,
                IngredienteId = nuevo.IngredienteId,
                Tipo = nuevo.Tipo,
                PrecioExtra = nuevo.Tipo == TipoIngrediente.Incluido ? 0 : nuevo.PrecioExtra
            });
        }

        if (imagen != null && modelo.Imagen != null)
        {
            imagenNueva = CrearObjectKey(RestauranteId, producto.Id, imagen.Extension);

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
                await CargarEditorAsync(modelo);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.RestauranteId == RestauranteId);

        if (producto != null)
        {
            producto.Activo = !producto.Activo;
            await _context.SaveChangesAsync();
            TempData["Ok"] = $"Producto {(producto.Activo ? "activado" : "desactivado")}";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task CargarEditorAsync(ProductoViewModel modelo)
    {
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == RestauranteId && c.Activa)
            .Include(c => c.Estacion)
            .OrderBy(c => c.Orden)
            .Select(c => new CategoriaViewModel
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Orden = c.Orden,
                Activa = c.Activa,
                EstacionId = c.EstacionId,
                EstacionNombre = c.Estacion!.Nombre
            })
            .ToListAsync();

        var ingredientes = await _context.Ingredientes
            .AsNoTracking()
            .Where(i => i.RestauranteId == RestauranteId && i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new IngredienteViewModel
            {
                Id = i.Id,
                Nombre = i.Nombre,
                Activo = i.Activo
            })
            .ToListAsync();

        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == RestauranteId && e.Activa)
            .OrderBy(e => e.Orden)
            .Select(e => new EstacionViewModel
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Orden = e.Orden,
                Activa = e.Activa
            })
            .ToListAsync();

        var seleccionados = modelo.IngredientesSeleccionados
            .GroupBy(i => i.IngredienteId)
            .ToDictionary(g => g.Key, g => g.Last());

        modelo.IngredientesSeleccionados = ingredientes
            .Select(i => seleccionados.TryGetValue(i.Id, out var actual)
                ? new ProductoIngredienteInputViewModel
                {
                    IngredienteId = i.Id,
                    NombreIngrediente = i.Nombre,
                    Seleccionado = actual.Seleccionado,
                    Tipo = actual.Tipo,
                    PrecioExtra = actual.PrecioExtra
                }
                : new ProductoIngredienteInputViewModel
                {
                    IngredienteId = i.Id,
                    NombreIngrediente = i.Nombre
                })
            .ToList();

        ViewBag.CategoriasEditor = categorias;
        ViewBag.EstacionesEditor = estaciones;
    }

    private async Task ValidarCatalogoAsync(ProductoViewModel modelo)
    {
        var categoriaValida = await _context.Categorias
            .AnyAsync(c => c.Id == modelo.CategoriaId &&
                           c.RestauranteId == RestauranteId &&
                           c.Activa);

        if (!categoriaValida)
            ModelState.AddModelError(nameof(modelo.CategoriaId), "Selecciona una categoría activa válida.");

        var seleccionados = modelo.IngredientesSeleccionados
            .Where(i => i.Seleccionado)
            .ToList();

        if (seleccionados.Count == 0)
            ModelState.AddModelError(string.Empty, "Selecciona al menos un ingrediente para el producto.");

        if (seleccionados.GroupBy(i => i.IngredienteId).Any(g => g.Count() > 1))
            ModelState.AddModelError(string.Empty, "Hay ingredientes duplicados en el producto.");

        var ids = seleccionados.Select(i => i.IngredienteId).Distinct().ToList();
        var validos = await _context.Ingredientes
            .Where(i => ids.Contains(i.Id) &&
                        i.RestauranteId == RestauranteId &&
                        i.Activo)
            .Select(i => i.Id)
            .ToListAsync();

        if (validos.Count != ids.Count)
            ModelState.AddModelError(string.Empty, "Uno o más ingredientes ya no están disponibles.");

        foreach (var item in seleccionados)
        {
            if (!TipoIngrediente.Todos().Contains(item.Tipo))
            {
                ModelState.AddModelError(string.Empty, "Tipo de ingrediente inválido.");
                continue;
            }

            if (item.Tipo == TipoIngrediente.Extra &&
                (item.PrecioExtra < 0 ||
                 item.PrecioExtra > 999999 ||
                 decimal.Truncate(item.PrecioExtra) != item.PrecioExtra))
            {
                ModelState.AddModelError(string.Empty,
                    "El precio extra debe ser un monto CLP válido entre $0 y $999.999.");
            }
        }
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
