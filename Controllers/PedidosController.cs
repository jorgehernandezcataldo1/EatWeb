using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Controllers;

[Authorize(Roles = "Admin,Garzon")]
public class PedidosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PedidoService _pedidoService;
    private readonly RestauranteContextService _restauranteContext;
    private readonly SesionService _sesionService;

    public PedidosController(
        ApplicationDbContext context,
        PedidoService pedidoService,
        RestauranteContextService restauranteContext,
        SesionService sesionService)
    {
        _context = context;
        _pedidoService = pedidoService;
        _restauranteContext = restauranteContext;
        _sesionService = sesionService;
    }


    private async Task<int?> GetRestauranteIdAsync()
    {
        var restaurante = await _restauranteContext.ObtenerActualAsync();
        return restaurante?.Id;
    }


    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        var userId = GetUserId();

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .Where(s =>
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId.Value &&
                (User.IsInRole("Admin") || s.Mesa.GarzonId == userId))
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
            .OrderBy(s => s.Mesa!.Numero)
            .ToListAsync();

        var vm = new PedidosIndexViewModel
        {
            Mesas = sesiones.Select(s => new PedidoMesaViewModel
            {
                SesionId = s.Id,
                MesaId = s.MesaId,
                MesaNumero = s.Mesa!.Numero,
                CuentaSolicitada = s.CuentaSolicitadaEn.HasValue,

                Pedidos = s.Comensales
                    .SelectMany(c => c.Pedidos.Select(p => new PedidoResumenViewModel
                    {
                        Id = p.Id,
                        ComensalNombre = c.Nombre,
                        Estado = p.Estado,
                        Total = p.Total,
                        FechaCreacion = p.FechaCreacion,

                        Detalles = p.Detalles.Select(d => new DetallePedidoResumenViewModel
                        {
                            Producto = d.NombreProducto,
                            Cantidad = d.Cantidad,
                            Subtotal = d.Subtotal,
                            Observacion = d.Observacion,

                            Personalizaciones = d.Ingredientes
                                .Select(i =>
                                    $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} " +
                                    $"{i.NombreIngrediente}" +
                                    (i.PrecioExtra > 0
                                        ? $" (+{i.PrecioExtra:C0})"
                                        : ""))
                                .ToList()

                        }).ToList()

                    }))
                    .OrderByDescending(p => p.FechaCreacion)
                    .ToList()

            })
            .Where(m => m.Pedidos.Count > 0 || m.CuentaSolicitada)
            .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
    int id,
    string nuevoEstado)
    {
        if (!EstadoPedido.Todos().Contains(nuevoEstado))
            return BadRequest();

        var restauranteId = await GetRestauranteIdAsync();

        if (!restauranteId.HasValue)
            return Forbid();

        var pedido = await _context.Pedidos
            .Include(p => p.MesaSesion)
                .ThenInclude(s => s!.Mesa)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.MesaSesion!.Mesa!.RestauranteId == restauranteId.Value);

        if (pedido == null)
            return NotFound();

        if (User.IsInRole("Garzon") &&
            pedido.MesaSesion!.Mesa!.GarzonId != GetUserId())
        {
            return Forbid();
        }

        var resultado = await _pedidoService.CambiarEstadoAsync(
            id,
            nuevoEstado,
            GetUserId() ?? string.Empty);

        if (!resultado.Ok)
            TempData["Error"] = string.Join(" ", resultado.Errores);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarMesa(int id)
    {
        var restauranteId = await GetRestauranteIdAsync();
        if (!restauranteId.HasValue) return Forbid();

        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .Include(s => s.Mesa)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId.Value &&
                (User.IsInRole("Admin") ||
                 User.IsInRole(Roles.AdminCadena) ||
                 User.IsInRole(Roles.AdminRestaurante) ||
                 s.Mesa.GarzonId == userId));

        if (sesion == null) return NotFound();

        var (ok, error) = await _sesionService.CerrarSesionAsync(
            id,
            forzar: false,
            usuarioId: userId);

        if (!ok)
        {
            TempData["Error"] = error ?? "No se pudo cerrar la mesa";
            return RedirectToAction(nameof(Cuenta), new { id });
        }

        TempData["Ok"] = $"Mesa {sesion.Mesa!.Numero} cerrada y liberada";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> Cuenta(int id)
    {
        var restauranteId = await GetRestauranteIdAsync();
        if (!restauranteId.HasValue) return Forbid();

        var userId = GetUserId();

        var sesion = await _context.MesaSesiones
            .AsNoTracking()
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId.Value &&
                (User.IsInRole("Admin") ||
                 User.IsInRole(Roles.AdminCadena) ||
                 User.IsInRole(Roles.AdminRestaurante) ||
                 s.Mesa.GarzonId == userId));

        if (sesion == null) return NotFound();

        // Solo pedidos no cancelados
        var pedidosValidos = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado != EstadoPedido.Cancelado)
            .ToList();

        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(c =>
            {
                var pedidosComensal = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .ToList();

                return new CuentaPersonaViewModel
                {
                    ComensalId = c.Id,
                    Nombre = c.Nombre,
                    FechaIngreso = c.FechaIngreso,
                    TotalConsumido = pedidosComensal.Sum(p => p.Total),
                    Items = pedidosComensal
                        .SelectMany(p => p.Detalles.Select(d => new CuentaItemViewModel
                        {
                            PedidoId = p.Id,
                            Producto = d.NombreProducto,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Subtotal = d.Subtotal,
                            Observacion = d.Observacion,
                            EstadoPedido = p.Estado,
                            Personalizaciones = d.Ingredientes
                                .Select(i => $"{(i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")} {i.NombreIngrediente}"
                                    + (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
                                .ToList()
                        }))
                        .ToList()
                };
            })
            .ToList();

        var total = personas.Sum(p => p.TotalConsumido);

        return View(new CuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            FechaApertura = sesion.FechaApertura,
            CuentaSolicitada = sesion.CuentaSolicitadaEn.HasValue,
            CuentaSolicitadaEn = sesion.CuentaSolicitadaEn,
            Total = total,
            TotalVerificado = personas.Sum(p => p.TotalConsumido),
            Personas = personas,
            Items = personas.SelectMany(p => p.Items).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Dividir(int id)
    {
        var restauranteId = await GetRestauranteIdAsync();
        if (!restauranteId.HasValue) return Forbid();

        var sesion = await _context.MesaSesiones
            .AsNoTracking()
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId.Value);

        if (sesion == null) return NotFound();

        var personas = sesion.Comensales
            .OrderBy(c => c.FechaIngreso)
            .Select(c => new CuentaPersonaViewModel
            {
                ComensalId = c.Id,
                Nombre = c.Nombre,
                FechaIngreso = c.FechaIngreso,
                TotalConsumido = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .Sum(p => p.Total)
            })
            .ToList();

        return View(new DivisionCuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            Total = personas.Sum(p => p.TotalConsumido),
            Personas = personas,
            Modo = "Igual",
            CantidadPartes = personas.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarConDivision(CerrarConDivisionInputViewModel modelo)
    {
        var restauranteId = await GetRestauranteIdAsync();
        if (!restauranteId.HasValue) return Forbid();

        var sesion = await _context.MesaSesiones
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
            .FirstOrDefaultAsync(s =>
                s.Id == modelo.SesionId &&
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == restauranteId.Value);

        if (sesion == null) return NotFound();

        // Guardar los grupos de pago (opcional, para registro)
        // Por ahora solo cerramos

        var (ok, error) = await _sesionService.CerrarSesionAsync(
            modelo.SesionId, forzar: false, usuarioId: GetUserId());

        if (!ok)
        {
            TempData["Error"] = error ?? "No se pudo cerrar la mesa";
            return RedirectToAction(nameof(Dividir), new { id = modelo.SesionId });
        }

        TempData["Ok"] = $"Mesa {sesion.Mesa!.Numero} cerrada";
        return RedirectToAction(nameof(Index));
    }
}
