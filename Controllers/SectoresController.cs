using EatWeb.Data;
using EatWeb.Models;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SectoresController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;

    public SectoresController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sectores = await _context.Sectores
            .AsNoTracking()
            .Where(s => s.RestauranteId == RestauranteId)
            .OrderBy(s => s.Orden)
            .ThenBy(s => s.Nombre)
            .Select(s => new SectorViewModel
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Orden = s.Orden,
                Activo = s.Activo,
                CantidadMesas = s.Mesas.Count
            })
            .ToListAsync();

        return View(sectores);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var ultimoOrden = await _context.Sectores
            .Where(s => s.RestauranteId == RestauranteId)
            .MaxAsync(s => (int?)s.Orden) ?? 0;

        var vm = new SectorViewModel
        {
            Orden = ultimoOrden + 1,
            Activo = true
        };

        await CargarMesasAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(SectorViewModel modelo)
    {
        await ValidarAsync(modelo);

        if (!ModelState.IsValid)
        {
            await CargarMesasAsync(modelo);
            return View(modelo);
        }

        var sector = new Sector
        {
            RestauranteId = RestauranteId,
            Nombre = modelo.Nombre.Trim(),
            Orden = modelo.Orden,
            Activo = modelo.Activo
        };

        _context.Sectores.Add(sector);
        await _context.SaveChangesAsync();

        await AsignarMesasAsync(sector.Id, modelo.MesaIds);

        TempData["Ok"] = $"Sector {sector.Nombre} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var sector = await _context.Sectores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.RestauranteId == RestauranteId);

        if (sector == null)
            return NotFound();

        var vm = new SectorViewModel
        {
            Id = sector.Id,
            Nombre = sector.Nombre,
            Orden = sector.Orden,
            Activo = sector.Activo,
            MesaIds = await _context.Mesas
                .Where(m => m.RestauranteId == RestauranteId && m.SectorId == sector.Id)
                .Select(m => m.Id)
                .ToListAsync()
        };

        await CargarMesasAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, SectorViewModel modelo)
    {
        if (id != modelo.Id)
            return BadRequest();

        var sector = await _context.Sectores
            .FirstOrDefaultAsync(s => s.Id == id && s.RestauranteId == RestauranteId);

        if (sector == null)
            return NotFound();

        await ValidarAsync(modelo);

        if (!ModelState.IsValid)
        {
            await CargarMesasAsync(modelo);
            return View(modelo);
        }

        sector.Nombre = modelo.Nombre.Trim();
        sector.Orden = modelo.Orden;
        sector.Activo = modelo.Activo;

        await _context.SaveChangesAsync();
        await AsignarMesasAsync(sector.Id, modelo.MesaIds);

        TempData["Ok"] = $"Sector {sector.Nombre} actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        var sector = await _context.Sectores
            .FirstOrDefaultAsync(s => s.Id == id && s.RestauranteId == RestauranteId);

        if (sector == null)
            return NotFound();

        sector.Activo = !sector.Activo;
        await _context.SaveChangesAsync();

        TempData["Ok"] = $"Sector {(sector.Activo ? "activado" : "desactivado")}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarAsync(SectorViewModel modelo)
    {
        var nombre = modelo.Nombre?.Trim() ?? string.Empty;

        if (await _context.Sectores.AnyAsync(s =>
            s.RestauranteId == RestauranteId &&
            s.Nombre == nombre &&
            s.Id != modelo.Id))
        {
            ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe un sector con ese nombre.");
        }

        var ids = modelo.MesaIds.Distinct().ToList();
        var mesasValidas = await _context.Mesas
            .CountAsync(m => m.RestauranteId == RestauranteId && ids.Contains(m.Id));

        if (mesasValidas != ids.Count)
            ModelState.AddModelError(nameof(modelo.MesaIds), "Hay una mesa inválida en la selección.");
    }

    private async Task CargarMesasAsync(SectorViewModel modelo)
    {
        var seleccionadas = modelo.MesaIds.ToHashSet();

        modelo.MesasDisponibles = await _context.Mesas
            .AsNoTracking()
            .Where(m => m.RestauranteId == RestauranteId)
            .OrderBy(m => m.Numero)
            .Select(m => new SectorMesaOpcionViewModel
            {
                Id = m.Id,
                Numero = m.Numero,
                Seleccionada = seleccionadas.Contains(m.Id),
                SectorActual = m.Sector != null ? m.Sector.Nombre : null
            })
            .ToListAsync();
    }

    private async Task AsignarMesasAsync(int sectorId, IEnumerable<int> mesaIds)
    {
        var seleccionadas = mesaIds.Distinct().ToHashSet();

        var mesas = await _context.Mesas
            .Where(m =>
                m.RestauranteId == RestauranteId &&
                (m.SectorId == sectorId || seleccionadas.Contains(m.Id)))
            .ToListAsync();

        foreach (var mesa in mesas)
        {
            if (seleccionadas.Contains(mesa.Id))
            {
                mesa.SectorId = sectorId;
            }
            else if (mesa.SectorId == sectorId)
            {
                mesa.SectorId = null;
            }
        }

        await _context.SaveChangesAsync();
    }
}
