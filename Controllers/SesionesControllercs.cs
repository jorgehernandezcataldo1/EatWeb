using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SesionesController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    public SesionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta)
    {
        // Default: últimos 7 días
        var fin = (hasta ?? DateTime.UtcNow).Date.AddDays(1);
        var inicio = (desde ?? fin.AddDays(-7)).Date;

        var sesiones = await _context.MesaSesiones
            .AsNoTracking()
            .Where(s => s.Mesa!.RestauranteId == RestauranteId
                        && s.FechaCierre != null
                        && s.FechaApertura >= inicio
                        && s.FechaApertura < fin)
            .OrderByDescending(s => s.FechaCierre)
            .Select(s => new SesionHistorialViewModel
            {
                Id = s.Id,
                MesaNumero = s.Mesa!.Numero,
                FechaApertura = s.FechaApertura,
                FechaCierre = s.FechaCierre,
                CuentaSolicitada = s.CuentaSolicitadaEn != null,
                Comensales = s.Comensales.Count(),
                TotalPedidos = s.Pedidos.Count(),
                Total = s.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .SelectMany(p => p.Detalles)
                    .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                    .Sum(d => d.Subtotal)
            })
            .ToListAsync();

        return View(new SesionesIndexViewModel
        {
            Desde = inicio,
            Hasta = fin.AddDays(-1),
            Sesiones = sesiones,
            TotalPeriodo = sesiones.Sum(s => s.Total),
            CantidadSesiones = sesiones.Count
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var sesion = await _context.MesaSesiones
            .AsNoTracking()
            .Include(s => s.Mesa)
            .Include(s => s.Comensales)
                .ThenInclude(c => c.Pedidos)
                    .ThenInclude(p => p.Detalles)
                        .ThenInclude(d => d.Ingredientes)
            .FirstOrDefaultAsync(s => s.Id == id
                                       && s.Mesa!.RestauranteId == RestauranteId);

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
                    .SelectMany(p => p.Detalles)
                    .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                    .Sum(d => d.Subtotal),
                Items = c.Pedidos
                    .Where(p => p.Estado != EstadoPedido.Cancelado)
                    .SelectMany(p => p.Detalles
                        .Where(d => d.Estado != EstadoDetallePedido.Cancelado)
                        .Select(d => new CuentaItemViewModel
                    {
                        PedidoId = p.Id,
                        Producto = d.NombreProducto,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario,
                        Subtotal = d.Subtotal,
                        Observacion = d.Observacion,
                        EstadoPedido = p.Estado,
                        Personalizaciones = d.Ingredientes
                            .Select(i =>
                                (i.Accion == AccionIngrediente.Quitar ? "Sin" : "Agregar")
                                + " " + i.NombreIngrediente
                                + (i.PrecioExtra > 0 ? $" (+{i.PrecioExtra:C0})" : ""))
                            .ToList()
                    }))
                    .ToList()
            })
            .ToList();

        return View(new CuentaViewModel
        {
            SesionId = sesion.Id,
            MesaNumero = sesion.Mesa!.Numero,
            FechaApertura = sesion.FechaApertura,
            CuentaSolicitada = sesion.CuentaSolicitadaEn.HasValue,
            CuentaSolicitadaEn = sesion.CuentaSolicitadaEn,
            Total = personas.Sum(p => p.TotalConsumido),
            TotalVerificado = personas.Sum(p => p.TotalConsumido),
            Personas = personas,
            Items = personas.SelectMany(p => p.Items).ToList()
        });
    }
}