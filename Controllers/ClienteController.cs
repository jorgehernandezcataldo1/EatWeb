using EatWeb.Data;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

// Los clientes NO tienen cuenta: se identifican por una cookie con su token de comensal.
[AllowAnonymous]
public class ClienteController : Controller
{
    private const string CookieComensal = "eatweb_comensal";

    private readonly ApplicationDbContext _context;
    private readonly SesionService _sesionService;

    public ClienteController(ApplicationDbContext context, SesionService sesionService)
    {
        _context = context;
        _sesionService = sesionService;
    }

    // Datos mínimos de la mesa escaneada (nada de entidades completas)
    private sealed record MesaQrInfo(
        int Id, int Numero, bool Activa, string RestauranteNombre, bool RestauranteActivo);

    // ---------- 1) Escaneo del QR: /m/{codigo} ----------
    [HttpGet]
    public async Task<IActionResult> Ingreso(string codigo)
    {
        var mesa = await BuscarMesaPorCodigoAsync(codigo);
        if (mesa == null)
            return View("Aviso", AvisoMesaInvalida());

        if (!mesa.Activa || !mesa.RestauranteActivo)
            return View("Aviso", AvisoMesaNoDisponible());

        // ¿Ya entró antes a ESTA mesa con este teléfono? Directo a la carta.
        var ctx = await LeerContextoAsync();
        if (ctx != null && ctx.MesaId == mesa.Id)
            return RedirectToAction(nameof(Carta));

        // Si venía de otra mesa (o su sesión se cerró), le pedimos el nombre otra vez
        // (precargado con el anterior).
        return View(new IngresoViewModel
        {
            MesaNumero = mesa.Numero,
            RestauranteNombre = mesa.RestauranteNombre,
            Nombre = ctx?.Nombre ?? string.Empty
        });
    }

    // ---------- 2) Envío del nombre ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ingreso(string codigo, string? nombre)
    {
        var mesa = await BuscarMesaPorCodigoAsync(codigo);
        if (mesa == null)
            return View("Aviso", AvisoMesaInvalida());

        if (!mesa.Activa || !mesa.RestauranteActivo)
            return View("Aviso", AvisoMesaNoDisponible());

        var nombreLimpio = LimpiarNombre(nombre);
        var vm = new IngresoViewModel
        {
            MesaNumero = mesa.Numero,
            RestauranteNombre = mesa.RestauranteNombre,
            Nombre = nombreLimpio
        };

        if (nombreLimpio.Length < 2 || nombreLimpio.Length > 40)
        {
            ModelState.AddModelError(nameof(IngresoViewModel.Nombre),
                "Escribe tu nombre (entre 2 y 40 caracteres)");
            return View(vm);
        }

        try
        {
            // Recién AQUÍ se abre la sesión de la mesa (si no existía) y se crea el comensal
            var sesion = await _sesionService.ObtenerOCrearSesionAbiertaAsync(mesa.Id);
            var token = await _sesionService.CrearComensalAsync(sesion.Id, nombreLimpio);
            EscribirCookieComensal(token);
        }
        catch (InvalidOperationException)
        {
            // Único caso previsto: se superó el máximo de comensales por sesión
            ModelState.AddModelError(nameof(IngresoViewModel.Nombre),
                "Esta mesa alcanzó el máximo de comensales. Avisa al personal.");
            return View(vm);
        }

        return RedirectToAction(nameof(Carta));
    }

    // ---------- 3) Carta ----------
    [HttpGet]
    public async Task<IActionResult> Carta()
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null)
            return RedirectToAction(nameof(SinSesion));

        // Una sola consulta, proyectada directo al ViewModel.
        // Solo categorías activas que tengan al menos un producto activo.
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => c.RestauranteId == ctx.RestauranteId
                        && c.Activa
                        && c.Productos.Any(p => p.Activo))
            .OrderBy(c => c.Orden)
            .Select(c => new CartaCategoriaViewModel
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Productos = c.Productos
                    .Where(p => p.Activo)
                    .OrderBy(p => p.Nombre)
                    .Select(p => new CartaProductoViewModel
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        Precio = p.Precio,
                        ImagenUrl = p.ImagenUrl,
                        Disponible = p.Disponible
                    })
                    .ToList()
            })
            .ToListAsync();

        return View(new CartaViewModel { Categorias = categorias });
    }

    // ---------- 4) Sin sesión (terminó la visita o nunca entró) ----------
    [HttpGet]
    public IActionResult SinSesion()
    {
        return View("Aviso", new AvisoClienteViewModel
        {
            Titulo = "Tu visita ha terminado",
            Mensaje = "Escanea nuevamente el código QR de tu mesa para continuar."
        });
    }

    // ---------- Helpers ----------

    /// <summary>Lee la cookie y busca al comensal. NO toca ViewData.</summary>
    private async Task<ContextoComensal?> LeerContextoAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieComensal, out var valor) ||
            !Guid.TryParse(valor, out var token))
            return null;

        // Devuelve null si la sesión se cerró o la mesa se desactivó
        return await _sesionService.ObtenerContextoAsync(token);
    }

    /// <summary>Igual que la anterior, pero además llena los datos que muestra el layout.</summary>
    private async Task<ContextoComensal?> ObtenerContextoAsync()
    {
        var ctx = await LeerContextoAsync();
        if (ctx != null)
        {
            ViewData["RestauranteNombre"] = ctx.RestauranteNombre;
            ViewData["MesaNumero"] = ctx.MesaNumero;
            ViewData["ComensalNombre"] = ctx.Nombre;
        }
        return ctx;
    }

    private void EscribirCookieComensal(Guid token)
    {
        Response.Cookies.Append(CookieComensal, token.ToString(), new CookieOptions
        {
            HttpOnly = true,                 // JavaScript no puede leerla
            IsEssential = true,
            SameSite = SameSiteMode.Lax,     // Lax: se envía al abrir el link del QR
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddHours(12)
        });
    }

    private Task<MesaQrInfo?> BuscarMesaPorCodigoAsync(string? codigo)
    {
        // Los códigos son Guid "N" (32 chars): no consultamos la BD con basura
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 32)
            return Task.FromResult<MesaQrInfo?>(null);

        return _context.Mesas
            .AsNoTracking()
            .Where(m => m.CodigoQr == codigo)
            .Select(m => new MesaQrInfo(
                m.Id, m.Numero, m.Activa, m.Restaurante!.Nombre, m.Restaurante!.Activo))
            .FirstOrDefaultAsync();
    }

    // Quita espacios de más: "  Jorge   Pérez " -> "Jorge Pérez"
    private static string LimpiarNombre(string? nombre) =>
        string.Join(' ', (nombre ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static AvisoClienteViewModel AvisoMesaInvalida() => new()
    {
        Titulo = "Código no válido",
        Mensaje = "No encontramos esta mesa. Escanea nuevamente el QR o avisa al personal."
    };

    private static AvisoClienteViewModel AvisoMesaNoDisponible() => new()
    {
        Titulo = "Mesa no disponible",
        Mensaje = "Esta mesa no está habilitada por el momento. Avisa al personal."
    };
}