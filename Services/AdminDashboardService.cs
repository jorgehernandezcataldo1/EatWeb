using EatWeb.Data;
using EatWeb.Models.Enums;
using EatWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class AdminDashboardService
{
    private readonly ApplicationDbContext _context;

    public AdminDashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminCadenaDashboardViewModel> ObtenerCadenaAsync(IReadOnlyCollection<int> restauranteIds)
    {
        var inicioHoy = ZonaHorariaService.InicioDelDiaUtc();

        if (restauranteIds.Count == 0)
            return new AdminCadenaDashboardViewModel();

        var cadenaNombre = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => restauranteIds.Contains(r.Id))
            .Select(r => r.Cadena != null ? r.Cadena.Nombre : null)
            .FirstOrDefaultAsync();

        var limiteAtraso = DateTime.UtcNow.AddMinutes(-10);

        var restaurantes = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => restauranteIds.Contains(r.Id))
            .OrderBy(r => r.Nombre)
            .Select(r => new RestauranteDashboardItemViewModel
            {
                Id = r.Id,
                Nombre = r.Nombre,
                Activo = r.Activo,
                TotalMesas = r.Mesas.Count(m => m.Activa),
                MesasOcupadas = r.Mesas.Count(m =>
                    m.Activa && m.Sesiones.Any(s => s.FechaCierre == null)),
                PedidosHoy = r.Mesas
                    .SelectMany(m => m.Sesiones)
                    .SelectMany(s => s.Pedidos)
                    .Count(p => p.FechaCreacion >= inicioHoy),
                SolicitudesPendientes = r.Mesas
                    .SelectMany(m => m.Sesiones)
                    .Where(s => s.FechaCierre == null)
                    .SelectMany(s => s.SolicitudesMesa)
                    .Count(s => s.Estado == EstadoSolicitudMesa.Pendiente),
                PedidosAtrasados = r.Mesas
                    .SelectMany(m => m.Sesiones)
                    .Where(s => s.FechaCierre == null)
                    .SelectMany(s => s.Pedidos)
                    .Count(p =>
                        p.FechaCreacion < limiteAtraso &&
                        p.Estado == EstadoPedido.Pendiente),
                VentasHoy = _context.Pagos
                    .Where(p =>
                        p.Estado == EstadoPago.Confirmado &&
                        p.FechaConfirmacion >= inicioHoy &&
                        p.Cuenta!.MesaSesion!.Mesa!.RestauranteId == r.Id)
                    .Sum(p => (decimal?)p.Monto) ?? 0
            })
            .ToListAsync();

        return new AdminCadenaDashboardViewModel
        {
            CadenaNombre = string.IsNullOrWhiteSpace(cadenaNombre) ? "Mi cadena" : cadenaNombre,
            Restaurantes = restaurantes,
            VentasHoy = restaurantes.Sum(r => r.VentasHoy),
            PedidosHoy = restaurantes.Sum(r => r.PedidosHoy),
            TotalMesas = restaurantes.Sum(r => r.TotalMesas),
            MesasOcupadas = restaurantes.Sum(r => r.MesasOcupadas),
            SolicitudesPendientes = restaurantes.Sum(r => r.SolicitudesPendientes)
        };
    }

    public async Task<AdminRestauranteDashboardViewModel?> ObtenerRestauranteAsync(int id)
    {
        var restaurante = await _context.Restaurantes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (restaurante == null)
            return null;

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
            .CountAsync(m => m.RestauranteId == id && m.Activa);

        var mesasOcupadas = await _context.Mesas
            .CountAsync(m =>
                m.RestauranteId == id &&
                m.Activa &&
                m.Sesiones.Any(s => s.FechaCierre == null));

        var totalPedidosHoy = await _context.Pedidos
            .CountAsync(p =>
                p.MesaSesion!.Mesa!.RestauranteId == id &&
                p.FechaCreacion >= inicioHoy);

        var pedidosActivosQuery = _context.Pedidos.Where(p =>
            p.MesaSesion!.FechaCierre == null &&
            p.MesaSesion.Mesa!.RestauranteId == id &&
            p.Estado != EstadoPedido.Entregado &&
            p.Estado != EstadoPedido.Cancelado);

        var pedidosActivos = await pedidosActivosQuery.CountAsync();
        var pedidosPendientes = await pedidosActivosQuery.CountAsync(p => p.Estado == EstadoPedido.Pendiente);
        var pedidosEnPreparacion = await pedidosActivosQuery.CountAsync(p => p.Estado == EstadoPedido.EnPreparacion);
        var pedidosListos = await pedidosActivosQuery.CountAsync(p => p.Estado == EstadoPedido.Listo);

        var solicitudesPendientes = await _context.SolicitudesMesa
            .CountAsync(s =>
                s.Estado == EstadoSolicitudMesa.Pendiente &&
                s.MesaSesion!.FechaCierre == null &&
                s.MesaSesion.Mesa!.RestauranteId == id);

        var pagosHoy = _context.Pagos.Where(p =>
            p.Estado == EstadoPago.Confirmado &&
            p.FechaConfirmacion >= inicioHoy &&
            p.Cuenta!.MesaSesion!.Mesa!.RestauranteId == id);

        var ventasHoy = await pagosHoy.SumAsync(p => (decimal?)p.Monto) ?? 0;
        var propinasHoy = await pagosHoy.SumAsync(p => (decimal?)p.Propina) ?? 0;

        var ahora = DateTime.UtcNow;
        var limiteAtrasoRestaurante = ahora.AddMinutes(-10);
        var limiteMesaLarga = ahora.AddMinutes(-90);

        var tiemposPreparacion = await _context.DetallesPedidos
            .AsNoTracking()
            .Where(d =>
                d.Pedido!.MesaSesion!.Mesa!.RestauranteId == id &&
                d.FechaInicioPreparacion >= inicioHoy &&
                d.FechaInicioPreparacion != null &&
                d.FechaListo != null)
            .Select(d => new
            {
                d.FechaInicioPreparacion,
                d.FechaListo
            })
            .ToListAsync();

        var tiempoPreparacionPromedio = tiemposPreparacion.Count == 0
            ? 0
            : tiemposPreparacion.Average(x =>
                (x.FechaListo!.Value - x.FechaInicioPreparacion!.Value).TotalMinutes);

        var tiemposSolicitudes = await _context.SolicitudesMesa
            .AsNoTracking()
            .Where(s =>
                s.MesaSesion!.Mesa!.RestauranteId == id &&
                s.FechaCreacion >= inicioHoy &&
                s.Estado == EstadoSolicitudMesa.Atendida &&
                s.FechaResolucion != null)
            .Select(s => new
            {
                s.FechaCreacion,
                s.FechaResolucion
            })
            .ToListAsync();

        var tiempoAtencionPromedio = tiemposSolicitudes.Count == 0
            ? 0
            : tiemposSolicitudes.Average(x =>
                (x.FechaResolucion!.Value - x.FechaCreacion).TotalMinutes);

        var pedidosAtrasados = await _context.Pedidos
            .AsNoTracking()
            .CountAsync(p =>
                p.MesaSesion!.Mesa!.RestauranteId == id &&
                p.MesaSesion.FechaCierre == null &&
                p.Estado == EstadoPedido.Pendiente &&
                p.FechaCreacion < limiteAtrasoRestaurante);

        var mesasMasDe90Min = await _context.MesaSesiones
            .AsNoTracking()
            .CountAsync(s =>
                s.Mesa!.RestauranteId == id &&
                s.FechaCierre == null &&
                s.FechaApertura < limiteMesaLarga);

        var estacionesBase = await _context.Estaciones.CountAsync(e =>
            e.RestauranteId == id &&
            e.Activa &&
            (e.Nombre == "Cocina" || e.Nombre == "Bar"));

        var categoriasActivas = await _context.Categorias.CountAsync(cat =>
            cat.RestauranteId == id &&
            cat.Activa &&
            cat.Estacion!.Activa);

        var garzonesActivos = await _context.RestaurantesMiembros.CountAsync(rm =>
            rm.RestauranteId == id &&
            rm.Rol == RolRestaurante.Garzon &&
            rm.Usuario.Activo);

        var mesasAsignadas = await _context.Mesas.CountAsync(m =>
            m.RestauranteId == id &&
            m.Activa &&
            m.GarzonId != null &&
            m.Garzon!.Activo);

        return new AdminRestauranteDashboardViewModel
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
            PedidosPendientes = pedidosPendientes,
            PedidosEnPreparacion = pedidosEnPreparacion,
            PedidosListos = pedidosListos,
            SolicitudesPendientes = solicitudesPendientes,
            VentasHoy = ventasHoy,
            PropinasHoy = propinasHoy,
            TiempoPreparacionPromedioMin = tiempoPreparacionPromedio,
            TiempoAtencionPromedioMin = tiempoAtencionPromedio,
            PedidosAtrasados = pedidosAtrasados,
            MesasMasDe90Min = mesasMasDe90Min,
            Checklist = new List<PilotChecklistItemViewModel>
            {
                new() { Titulo = "Estaciones", Descripcion = "Cocina y Bar activas", Ok = estacionesBase >= 2, Controller = "Estaciones" },
                new() { Titulo = "Carta", Descripcion = "Al menos una categoría activa", Ok = categoriasActivas > 0, Controller = "Categorias" },
                new() { Titulo = "Productos", Descripcion = "Al menos un producto disponible", Ok = productosDisponibles > 0, Controller = "Productos" },
                new() { Titulo = "Mesas", Descripcion = "Al menos una mesa activa con QR", Ok = totalMesas > 0, Controller = "Mesas", Action = "Administrar" },
                new() { Titulo = "Garzones", Descripcion = "Al menos un garzón activo", Ok = garzonesActivos > 0, Controller = "Garzones" },
                new() { Titulo = "Asignación", Descripcion = "Al menos una mesa activa asignada a un garzón", Ok = mesasAsignadas > 0, Controller = "Mesas", Action = "Administrar" }
            }
        };
    }
}
