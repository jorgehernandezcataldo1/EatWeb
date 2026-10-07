using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Controllers;

// Admin y Garzón entran al controller; las acciones de gestión exigen además Admin.
[Authorize(Roles = Roles.AdminOGarzon)]
public class MesasController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public MesasController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }


    // ---------- Tablero: el garzón ve sus mesas, el admin todas ----------
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var restauranteId = RestauranteId;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.Mesas.Where(m => m.RestauranteId == restauranteId);

        if (User.IsInRole(Roles.Garzon))
        {
            query = query.Where(m =>
                m.GarzonId == userId ||
                m.Sesiones.Any(s => s.FechaCierre == null && s.GarzonId == userId));
        }

        var mesas = await ConstruirTableroAsync(query);
        var solicitudes = await ObtenerSolicitudesPendientesAsync(userId);

        return View(new MesasIndexViewModel
        {
            Mesas = mesas,
            SolicitudesPendientes = solicitudes
        });
    }

    // ---------- Gestión (solo Admin) ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Administrar()
    {
        var restauranteId = RestauranteId;
        var query = _context.Mesas.Where(m => m.RestauranteId == restauranteId);
        return View(await ConstruirTableroAsync(query));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var restauranteId = RestauranteId;
        var ultimo = await _context.Mesas
            .Where(m => m.RestauranteId == restauranteId)
            .MaxAsync(m => (int?)m.Numero) ?? 0;

        await CargarOpcionesMesaAsync(restauranteId);
        return View(new MesaFormViewModel { Numero = ultimo + 1 });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(MesaFormViewModel modelo)
    {
        var restauranteId = RestauranteId;

        if (await _context.Mesas.AnyAsync(m => m.RestauranteId == restauranteId && m.Numero == modelo.Numero))
            ModelState.AddModelError(nameof(modelo.Numero), "Ya existe una mesa con ese número");

        if (!await GarzonEsValidoAsync(modelo.GarzonId, restauranteId))
            ModelState.AddModelError(nameof(modelo.GarzonId), "Garzón inválido");

        if (!await SectorEsValidoAsync(modelo.SectorId, restauranteId))
            ModelState.AddModelError(nameof(modelo.SectorId), "Sector inválido");

        if (!ModelState.IsValid)
        {
            await CargarOpcionesMesaAsync(restauranteId, modelo.SectorId);
            return View(modelo);
        }

        var mesa = new Mesa
        {
            RestauranteId = restauranteId,
            Numero = modelo.Numero,
            CodigoQr = QrHelper.GenerarCodigo(),
            Activa = modelo.Activa,
            GarzonId = string.IsNullOrEmpty(modelo.GarzonId) ? null : modelo.GarzonId,
            SectorId = modelo.SectorId,
            OrdenEnSector = modelo.OrdenEnSector
        };

        _context.Mesas.Add(mesa);
        await _context.SaveChangesAsync();

        TempData["Ok"] = $"Mesa {mesa.Numero} creada. Imprime su QR.";
        return RedirectToAction(nameof(QrVista), new { id = mesa.Id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        await CargarOpcionesMesaAsync(mesa.RestauranteId, mesa.SectorId);
        return View(new MesaFormViewModel
        {
            Id = mesa.Id,
            Numero = mesa.Numero,
            Activa = mesa.Activa,
            GarzonId = mesa.GarzonId,
            SectorId = mesa.SectorId,
            OrdenEnSector = mesa.OrdenEnSector
        });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, MesaFormViewModel modelo)
    {
        if (id != modelo.Id) return BadRequest();

        var restauranteId = RestauranteId;
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        if (await _context.Mesas.AnyAsync(m => m.RestauranteId == restauranteId && m.Numero == modelo.Numero && m.Id != id))
            ModelState.AddModelError(nameof(modelo.Numero), "Ya existe otra mesa con ese número");

        if (!await GarzonEsValidoAsync(modelo.GarzonId, restauranteId))
            ModelState.AddModelError(nameof(modelo.GarzonId), "Garzón inválido");
 
        if (!await SectorEsValidoAsync(modelo.SectorId, restauranteId))
            ModelState.AddModelError(nameof(modelo.SectorId), "Sector inválido");

        if (!modelo.Activa && await TieneSesionAbiertaAsync(id))
            ModelState.AddModelError(nameof(modelo.Activa),
                "No puedes desactivar una mesa con clientes sentados. Cierra la sesión primero.");

        if (!ModelState.IsValid)
        {
            await CargarOpcionesMesaAsync(restauranteId, modelo.SectorId);
            return View(modelo);
        }

        mesa.Numero = modelo.Numero;
        mesa.Activa = modelo.Activa;
        mesa.GarzonId = string.IsNullOrEmpty(modelo.GarzonId) ? null : modelo.GarzonId;
        mesa.SectorId = modelo.SectorId;
        mesa.OrdenEnSector = modelo.OrdenEnSector;
        await _context.SaveChangesAsync();

        TempData["Ok"] = "Mesa actualizada";
        return RedirectToAction(nameof(Administrar));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        if (mesa.Activa && await TieneSesionAbiertaAsync(id))
        {
            TempData["Error"] = "No puedes desactivar una mesa con clientes sentados.";
        }
        else
        {
            mesa.Activa = !mesa.Activa;
            await _context.SaveChangesAsync();
            TempData["Ok"] = $"Mesa {mesa.Numero} {(mesa.Activa ? "activada" : "desactivada")}";
        }

        return RedirectToAction(nameof(Administrar));
    }

    // ---------- QR ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> QrVista(int id)
    {
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        return View(new MesaQrViewModel
        {
            Id = mesa.Id,
            Numero = mesa.Numero,
            Url = ConstruirUrlQr(mesa.CodigoQr)
        });
    }

    // Devuelve la imagen PNG (la usa <img src="/Mesas/Qr/5">)
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Qr(int id)
    {
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        return File(QrHelper.GenerarPng(ConstruirUrlQr(mesa.CodigoQr)), "image/png");
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerarQr(int id)
    {
        var mesa = await BuscarMesaAsync(id);
        if (mesa == null) return NotFound();

        mesa.CodigoQr = QrHelper.GenerarCodigo();
        await _context.SaveChangesAsync();

        TempData["Ok"] = "Código regenerado. El QR anterior ya no funciona: imprime el nuevo.";
        return RedirectToAction(nameof(QrVista), new { id });
    }

    // ---------- Helpers privados ----------
    private Task<Mesa?> BuscarMesaAsync(int id)
    {
        var restauranteId = RestauranteId;
        return _context.Mesas.FirstOrDefaultAsync(m => m.Id == id && m.RestauranteId == restauranteId);
    }

    private Task<bool> TieneSesionAbiertaAsync(int mesaId) =>
        _context.MesaSesiones.AnyAsync(s => s.MesaId == mesaId && s.FechaCierre == null);

    private string ConstruirUrlQr(string codigo)
    {
        // Si App:BaseUrl está vacío usa el host actual (en un celular, "localhost" no sirve:
        // configura BaseUrl con la IP de tu PC o la URL pública).
        var baseUrl = _configuration["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";

        return $"{baseUrl.TrimEnd('/')}/m/{Uri.EscapeDataString(codigo)}";
    }

    private IQueryable<ApplicationUser> GarzonesActivos(int restauranteId)
    {
        return _context.RestaurantesMiembros
            .Where(rm =>
                rm.RestauranteId == restauranteId &&
                rm.Rol == RolRestaurante.Garzon &&
                rm.Usuario.Activo)
            .Select(rm => rm.Usuario);
    }

    private async Task<bool> GarzonEsValidoAsync(string? garzonId, int restauranteId)
    {
        if (string.IsNullOrEmpty(garzonId)) return true; // "sin asignar" es válido
        return await GarzonesActivos(restauranteId).AnyAsync(u => u.Id == garzonId);
    }

    private Task<bool> SectorEsValidoAsync(int? sectorId, int restauranteId)
    {
        if (!sectorId.HasValue) return Task.FromResult(true);

        return _context.Sectores.AnyAsync(s =>
            s.Id == sectorId.Value &&
            s.RestauranteId == restauranteId &&
            s.Activo);
    }

    private async Task CargarOpcionesMesaAsync(int restauranteId, int? sectorId = null)
    {
        var garzones = await GarzonesActivos(restauranteId)
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new { u.Id, u.NombreCompleto })
            .ToListAsync();

        var sectores = await _context.Sectores
            .AsNoTracking()
            .Where(s => s.RestauranteId == restauranteId && s.Activo)
            .OrderBy(s => s.Orden)
            .ThenBy(s => s.Nombre)
            .ToListAsync();

        ViewBag.Garzones = new SelectList(garzones, "Id", "NombreCompleto");
        ViewBag.Sectores = new SelectList(sectores, "Id", "Nombre", sectorId);
    }

    private Task<List<SolicitudMesaResumenViewModel>> ObtenerSolicitudesPendientesAsync(string? userId)
    {
        return _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.Estado == EstadoSolicitudMesa.Pendiente &&
                s.MesaSesion!.FechaCierre == null &&
                s.MesaSesion.Mesa!.RestauranteId == RestauranteId &&
                (User.EsAdmin() ||
                 s.MesaSesion.GarzonId == userId ||
                 s.MesaSesion.Mesa.GarzonId == userId))
            .OrderBy(s => s.FechaCreacion)
            .Select(s => new SolicitudMesaResumenViewModel
            {
                Id = s.Id,
                SesionId = s.MesaSesionId,
                MesaNumero = s.MesaSesion!.Mesa!.Numero,
                ComensalNombre = s.Comensal!.Nombre,
                Tipo = s.Tipo,
                Mensaje = s.Mensaje,
                Estado = s.Estado,
                FechaCreacion = s.FechaCreacion,
                FechaResolucion = s.FechaResolucion
            })
            .ToListAsync();
    }

    /// <summary>
    /// Arma el tablero con consultas pequeñas (mesas, sesiones abiertas, pedidos)
    /// y combina en memoria. Es más claro que un Include gigante y trae solo lo necesario.
    /// </summary>
    private async Task<List<MesaViewModel>> ConstruirTableroAsync(IQueryable<Mesa> query)
    {
        var mesas = await query
            .AsNoTracking()
            .OrderBy(m => m.Sector != null ? m.Sector.Orden : int.MaxValue)
            .ThenBy(m => m.OrdenEnSector)
            .ThenBy(m => m.Numero)
            .Select(m => new
            {
                m.Id,
                m.Numero,
                m.Activa,
                m.SectorId,
                m.OrdenEnSector,
                SectorNombre = m.Sector != null ? m.Sector.Nombre : "Sin sector",
                SectorOrden = m.Sector != null ? m.Sector.Orden : int.MaxValue,
                GarzonNombre = m.Garzon != null ? m.Garzon.NombreCompleto : null
            })
            .ToListAsync();

        var mesaIds = mesas.Select(m => m.Id).ToList();

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .Where(s => s.FechaCierre == null && mesaIds.Contains(s.MesaId))
            .Select(s => new
            {
                s.Id,
                s.MesaId,
                s.FechaApertura,
                CuentaSolicitada = s.CuentaSolicitadaEn != null,
                Comensales = s.Comensales.Count()
            })
            .ToListAsync();

        var sesionIds = sesiones.Select(s => s.Id).ToList();

        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => sesionIds.Contains(p.MesaSesionId))
            .Select(p => new { p.MesaSesionId, p.Estado, p.Total })
            .ToListAsync();

        var solicitudesPorSesion = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                sesionIds.Contains(s.MesaSesionId) &&
                s.Estado == EstadoSolicitudMesa.Pendiente)
            .GroupBy(s => s.MesaSesionId)
            .Select(g => new { SesionId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.SesionId, x => x.Cantidad);

        return mesas.Select(m =>
        {
            var sesion = sesiones.FirstOrDefault(s => s.MesaId == m.Id);
            var pedidosMesa = pedidos
                .Where(p => sesion != null && p.MesaSesionId == sesion.Id)
                .ToList();

            return new MesaViewModel
            {
                Id = m.Id,
                Numero = m.Numero,
                Activa = m.Activa,
                GarzonNombre = m.GarzonNombre,
                TieneSesionAbierta = sesion != null,
                SesionId = sesion?.Id,
                SectorId = m.SectorId,
                SectorNombre = m.SectorNombre,
                OrdenEnSector = m.OrdenEnSector,
                FechaApertura = sesion?.FechaApertura,
                Comensales = sesion?.Comensales ?? 0,
                SolicitudesPendientes = sesion == null
                    ? 0
                    : solicitudesPorSesion.GetValueOrDefault(sesion.Id),
                TotalAcumulado = pedidosMesa
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .Sum(p => p.Total),
                Estado = MesaEstadoService.Calcular(
                    m.Activa,
                    sesion != null,
                    sesion?.CuentaSolicitada ?? false,
                    pedidosMesa.Select(p => p.Estado))
            };
        }).ToList();
    }
}