using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.Services.Storage;
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
    private readonly CarritoService _carrito;
    private readonly PedidoService _pedidoService;
    private readonly SolicitudMesaService _solicitudMesaService;
    private readonly IStorageService _storage;

    public ClienteController(
        ApplicationDbContext context,
        SesionService sesionService,
        CarritoService carrito,
        PedidoService pedidoService,
        SolicitudMesaService solicitudMesaService,
        IStorageService storage)
    {
        _context = context;
        _sesionService = sesionService;
        _carrito = carrito;
        _pedidoService = pedidoService;
        _solicitudMesaService = solicitudMesaService;
        _storage = storage;
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
                        ImagenKey = p.ImagenKey,
                        Disponible = p.Disponible
                    })
                    .ToList()
            })
            .ToListAsync();

        foreach (var categoria in categorias)
        {
            foreach (var producto in categoria.Productos)
                producto.ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey);
        }

        return View(new CartaViewModel { Categorias = categorias });
    }

    // ---------- 3b) Detalle del producto ----------
    [HttpGet]
    public async Task<IActionResult> Producto(int id)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        var producto = await _context.Productos
            .AsNoTracking()
            .Where(p => p.Id == id && p.RestauranteId == ctx.RestauranteId && p.Activo)
            .Select(p => new ProductoDetalleViewModel
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                ImagenKey = p.ImagenKey,
                Disponible = p.Disponible,
                Incluidos = p.Ingredientes
                    .Where(pi => pi.Tipo == TipoIngrediente.Incluido && pi.Ingrediente!.Activo)
                    .Select(pi => new IngredienteOpcionViewModel
                    {
                        IngredienteId = pi.IngredienteId,
                        Nombre = pi.Ingrediente!.Nombre
                    })
                    .ToList(),
                Extras = p.Ingredientes
                    .Where(pi => pi.Tipo == TipoIngrediente.Extra && pi.Ingrediente!.Activo)
                    .Select(pi => new IngredienteOpcionViewModel
                    {
                        IngredienteId = pi.IngredienteId,
                        Nombre = pi.Ingrediente!.Nombre,
                        PrecioExtra = pi.PrecioExtra
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (producto == null) return NotFound();

        producto.ImagenUrl = _storage.ObtenerUrlPublica(producto.ImagenKey);
        return View(producto);
    }

    // ---------- 3c) Agregar al carrito ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarAlCarrito(AgregarCarritoInputViewModel modelo)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        if (modelo.Cantidad < 1 || modelo.Cantidad > 20)
        {
            TempData["Error"] = "Cantidad inválida";
            return RedirectToAction(nameof(Producto), new { id = modelo.ProductoId });
        }

        _carrito.Agregar(
            ctx.ComensalId,
            modelo.ProductoId,
            modelo.Cantidad,
            modelo.IngredientesQuitar ?? new List<int>(),
            modelo.IngredientesAgregar ?? new List<int>(),
            modelo.Observacion ?? string.Empty);

        TempData["Ok"] = "Agregado al carrito";
        return RedirectToAction(nameof(Carta));
    }

    // ---------- 3d) Ver carrito ----------
    [HttpGet]
    public async Task<IActionResult> Carrito()
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        var carrito = _carrito.Obtener(ctx.ComensalId);
        var calculado = await _pedidoService.CalcularCarritoAsync(carrito, ctx.RestauranteId);

        return View(new CarritoViewModel
        {
            Lineas = calculado.Lineas,
            Total = calculado.Total,
            Errores = calculado.Errores
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarCantidad(Guid lineaId, int delta)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        _carrito.CambiarCantidad(ctx.ComensalId, lineaId, delta);
        return RedirectToAction(nameof(Carrito));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarLinea(Guid lineaId)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        _carrito.Quitar(ctx.ComensalId, lineaId);
        return RedirectToAction(nameof(Carrito));
    }

    // ---------- 3e) Enviar pedido ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarPedido(string? observacionGeneral)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        var carrito = _carrito.Obtener(ctx.ComensalId);
        var resultado = await _pedidoService.CrearPedidoAsync(
            ctx.ComensalId, carrito, observacionGeneral);

        if (!resultado.Ok)
        {
            TempData["Error"] = string.Join(" ", resultado.Errores);
            return RedirectToAction(nameof(Carrito));
        }

        _carrito.Vaciar(ctx.ComensalId);
        TempData["Ok"] = "Pedido enviado a cocina";
        return RedirectToAction(nameof(MiMesa));
    }

    // ---------- 3f) Mi Mesa ----------
    [HttpGet]
    public async Task<IActionResult> MiMesa()
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null) return RedirectToAction(nameof(SinSesion));

        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.ComensalId == ctx.ComensalId
                        && p.Estado != EstadoPedido.Cancelado)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new MiPedidoViewModel
            {
                Id = p.Id,
                Estado = p.Estado,
                Total = p.Detalles
                    .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                    .Sum(d => d.Subtotal),
                Fecha = p.FechaCreacion,
                Items = p.Detalles
                    .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                    .Select(d => d.Cantidad + "x " + d.NombreProducto)
                    .ToList()
            })
            .ToListAsync();

        var solicitudes = await ObtenerSolicitudesAsync(ctx);

        return View(new MiMesaViewModel
        {
            MesaNumero = ctx.MesaNumero,
            ComensalNombre = ctx.Nombre,
            CuentaSolicitada = ctx.CuentaSolicitada,
            TotalConsumido = pedidos.Sum(p => p.Total),
            Pedidos = pedidos,
            Solicitudes = solicitudes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearSolicitud(string tipo, string? mensaje)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null)
            return RedirectToAction(nameof(SinSesion));

        var resultado = await _solicitudMesaService.CrearAsync(
            ctx.SesionId,
            ctx.ComensalId,
            tipo,
            mensaje);

        if (!resultado.Ok)
        {
            TempData["Error"] = string.Join(" ", resultado.Errores);
            return RedirectToAction(nameof(MiMesa));
        }

        TempData["Ok"] = resultado.YaExistia
            ? "Ya tienes una solicitud pendiente de este tipo."
            : tipo switch
            {
                TipoSolicitudMesa.PedirCuenta => "Cuenta solicitada. El garzón te atenderá pronto.",
                TipoSolicitudMesa.LlamarGarzon => "Llamaste al garzón. Te atenderá pronto.",
                _ => "Solicitud enviada al garzón."
            };

        return RedirectToAction(nameof(MiMesa));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SolicitarCuenta() =>
        CrearSolicitud(TipoSolicitudMesa.PedirCuenta, null);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarSolicitud(int id)
    {
        var ctx = await ObtenerContextoAsync();
        if (ctx == null)
            return RedirectToAction(nameof(SinSesion));

        var resultado = await _solicitudMesaService.CancelarAsync(id, ctx.ComensalId);
        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);
        else
            TempData["Ok"] = "Solicitud cancelada.";

        return RedirectToAction(nameof(MiMesa));
    }

    [HttpGet]
    public async Task<IActionResult> MisSolicitudes()
    {
        var ctx = await LeerContextoAsync();
        if (ctx == null)
            return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        var solicitudes = await ObtenerSolicitudesAsync(ctx);
        return PartialView("_MisSolicitudes", solicitudes);
    }

    private Task<List<SolicitudMesaResumenViewModel>> ObtenerSolicitudesAsync(ContextoComensal ctx)
    {
        return _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.MesaSesionId == ctx.SesionId &&
                s.ComensalId == ctx.ComensalId)
            .OrderByDescending(s => s.FechaCreacion)
            .Take(10)
            .Select(s => new SolicitudMesaResumenViewModel
            {
                Id = s.Id,
                SesionId = s.MesaSesionId,
                MesaNumero = ctx.MesaNumero,
                ComensalNombre = ctx.Nombre,
                Tipo = s.Tipo,
                Mensaje = s.Mensaje,
                Estado = s.Estado,
                FechaCreacion = s.FechaCreacion,
                FechaResolucion = s.FechaResolucion
            })
            .ToListAsync();
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