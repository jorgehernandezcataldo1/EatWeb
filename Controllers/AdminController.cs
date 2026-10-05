using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using EatWeb.ViewModels;
using EatWeb.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AccesoRestauranteService _acceso;
    private readonly RestauranteContextService _restauranteContext;


    public AdminController(
        ApplicationDbContext context,
        AccesoRestauranteService acceso,
        RestauranteContextService restauranteContext)
    {
        _context = context;
        _acceso = acceso;
        _restauranteContext = restauranteContext;
    }

    [HttpGet("SeleccionarRestaurante/{id:int}")]
    public async Task<IActionResult> SeleccionarRestaurante(int id)
    {
        var seleccionado = await _restauranteContext.SeleccionarAsync(id);

        if (!seleccionado)
            return Forbid();

        return RedirectToAction(nameof(Restaurante), new { id });
    }


    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var restaurantes = await _acceso
            .ObtenerRestaurantesAccesiblesAsync();

        if (!restaurantes.Any())
            return Forbid();

        // Si es administrador de restaurante,
        // tiene un solo restaurante.
        if (User.IsInRole(Roles.AdminRestaurante))
        {
            var restaurante = restaurantes.SingleOrDefault();

            if (restaurante == null)
                return Forbid();

            return RedirectToAction(
                nameof(Restaurante),
                new { id = restaurante.Id });
        }

        // AdminCadena
        var datos = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => restaurantes.Select(x => x.Id).Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.Nombre,
                TotalProductos = r.Productos.Count(),
                TotalMesas = r.Mesas.Count(),
                TotalPedidos = r.Mesas
                    .SelectMany(m => m.Sesiones)
                    .SelectMany(s => s.Pedidos)
                    .Count()
            })
            .OrderBy(r => r.Nombre)
            .ToListAsync();

        return View("IndexCadena", datos);
    }

    [HttpGet("Restaurante/{id:int}")]
    public async Task<IActionResult> Restaurante(int id)
    {
        if (!await _acceso.PuedeAccederAsync(id))
            return Forbid();

        var restaurante = await _context.Restaurantes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (restaurante == null)
            return NotFound();

        var inicioHoy = ZonaHorariaService.InicioDelDiaUtc();

        var totalProductos = await _context.Productos
            .CountAsync(p => p.RestauranteId == id);

        var productosDisponibles = await _context.Productos
            .CountAsync(p =>
                p.RestauranteId == id &&
                p.Activo &&
                p.Disponible &&
                p.Categoria!.Activa &&
                p.Categoria.Estacion!.Activa);

        var totalMesas = await _context.Mesas
            .CountAsync(m => m.RestauranteId == id);

        var mesasActivas = await _context.Mesas
            .CountAsync(m => m.RestauranteId == id && m.Activa);

        var mesasOcupadas = await _context.MesaSesiones
            .CountAsync(s =>
                s.FechaCierre == null &&
                s.Mesa!.RestauranteId == id);

        var totalPedidosHoy = await _context.Pedidos
            .CountAsync(p =>
                p.MesaSesion!.Mesa!.RestauranteId == id &&
                p.FechaCreacion >= inicioHoy);

        var pedidosActivos = await _context.Pedidos
            .CountAsync(p =>
                p.MesaSesion!.FechaCierre == null &&
                p.MesaSesion.Mesa!.RestauranteId == id &&
                p.Estado != EstadoPedido.Entregado &&
                p.Estado != EstadoPedido.Cancelado);

        var solicitudesPendientes = await _context.SolicitudesMesa
            .CountAsync(s =>
                s.Estado == EstadoSolicitudMesa.Pendiente &&
                s.MesaSesion!.FechaCierre == null &&
                s.MesaSesion.Mesa!.RestauranteId == id);

        var ventasHoy = await _context.Pagos
            .Where(p =>
                p.Estado == EstadoPago.Confirmado &&
                p.FechaConfirmacion >= inicioHoy &&
                p.Cuenta!.MesaSesion!.Mesa!.RestauranteId == id)
            .SumAsync(p => (decimal?)p.Monto) ?? 0;

        var propinasHoy = await _context.Pagos
            .Where(p =>
                p.Estado == EstadoPago.Confirmado &&
                p.FechaConfirmacion >= inicioHoy &&
                p.Cuenta!.MesaSesion!.Mesa!.RestauranteId == id)
            .SumAsync(p => (decimal?)p.Propina) ?? 0;

        var estacionesBase = await _context.Estaciones
            .CountAsync(e =>
                e.RestauranteId == id &&
                e.Activa &&
                (e.Nombre == "Cocina" || e.Nombre == "Bar"));

        var categoriasActivas = await _context.Categorias
            .CountAsync(cat =>
                cat.RestauranteId == id &&
                cat.Activa &&
                cat.Estacion!.Activa);

        var garzonesActivos = await _context.RestaurantesMiembros
            .CountAsync(rm =>
                rm.RestauranteId == id &&
                rm.Rol == RolRestaurante.Garzon &&
                rm.Usuario.Activo);

        var mesasAsignadas = await _context.Mesas
            .CountAsync(m =>
                m.RestauranteId == id &&
                m.Activa &&
                m.GarzonId != null &&
                m.Garzon!.Activo);

        var modelo = new AdminRestauranteDashboardViewModel
        {
            RestauranteId = restaurante.Id,
            RestauranteNombre = restaurante.Nombre,
            RestauranteActivo = restaurante.Activo,
            TotalProductos = totalProductos,
            ProductosDisponibles = productosDisponibles,
            TotalMesas = totalMesas,
            MesasOcupadas = mesasOcupadas,
            TotalPedidosHoy = totalPedidosHoy,
            PedidosActivos = pedidosActivos,
            SolicitudesPendientes = solicitudesPendientes,
            VentasHoy = ventasHoy,
            PropinasHoy = propinasHoy,
            Checklist = new List<PilotChecklistItemViewModel>
            {
                new()
                {
                    Titulo = "Estaciones",
                    Descripcion = "Cocina y Bar activas",
                    Ok = estacionesBase >= 2,
                    Controller = "Estaciones"
                },
                new()
                {
                    Titulo = "Carta",
                    Descripcion = "Al menos una categoría activa",
                    Ok = categoriasActivas > 0,
                    Controller = "Categorias"
                },
                new()
                {
                    Titulo = "Productos",
                    Descripcion = "Al menos un producto disponible",
                    Ok = productosDisponibles > 0,
                    Controller = "Productos"
                },
                new()
                {
                    Titulo = "Mesas",
                    Descripcion = "Al menos una mesa activa con QR",
                    Ok = mesasActivas > 0,
                    Controller = "Mesas",
                    Action = "Administrar"
                },
                new()
                {
                    Titulo = "Garzones",
                    Descripcion = "Al menos un garzón activo",
                    Ok = garzonesActivos > 0,
                    Controller = "Garzones"
                },
                new()
                {
                    Titulo = "Asignación",
                    Descripcion = "Al menos una mesa activa asignada a un garzón",
                    Ok = mesasAsignadas > 0,
                    Controller = "Mesas",
                    Action = "Administrar"
                }
            }
        };

        ViewData["RestauranteNombre"] = restaurante.Nombre;
        return View("Restaurante", modelo);
    }

}
