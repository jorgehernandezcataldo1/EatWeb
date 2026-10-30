using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

/// <summary>Administración global. Nunca hereda RestauranteControllerBase.</summary>
[Authorize(Roles = Roles.SuperAdmin)]
[Route("SuperAdmin")]
public sealed class SuperAdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RestauranteDefaultsService _defaults;

    public SuperAdminController(ApplicationDbContext db,
        UserManager<ApplicationUser> users, RestauranteDefaultsService defaults)
    {
        _db = db;
        _users = users;
        _defaults = defaults;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var cadenas = await _db.Cadenas.AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre, Restaurantes = c.Restaurantes.Count() })
            .ToListAsync();
        var restaurantes = await _db.Restaurantes.AsNoTracking()
            .OrderBy(r => r.Nombre)
            .Select(r => new { r.Id, r.Nombre, r.CadenaId, Cadena = r.Cadena != null ? r.Cadena.Nombre : "Independiente", r.Activo })
            .ToListAsync();
        var miembrosCadena = await _db.CadenasMiembros.AsNoTracking()
            .OrderBy(m => m.CadenaId)
            .Select(m => new { m.CadenaId, m.UsuarioId, Email = m.Usuario.Email, m.Rol })
            .ToListAsync();
        var miembrosRestaurante = await _db.RestaurantesMiembros.AsNoTracking()
            .OrderBy(m => m.RestauranteId)
            .Select(m => new { m.RestauranteId, m.UsuarioId, Email = m.Usuario.Email, m.Rol, m.Activo })
            .ToListAsync();

        ViewBag.Cadenas = cadenas;
        ViewBag.Restaurantes = restaurantes;
        ViewBag.MiembrosCadena = miembrosCadena;
        ViewBag.MiembrosRestaurante = miembrosRestaurante;
        return View();
    }

    [HttpPost("Cadena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCadena(int? id, string nombre)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length is < 2 or > 120)
            return BadRequest("El nombre debe tener entre 2 y 120 caracteres.");

        Cadena? cadena = null;
        if (id.HasValue)
        {
            cadena = await _db.Cadenas.FindAsync(id.Value);
            if (cadena == null) return NotFound();
            cadena.Nombre = nombre;
        }
        else
        {
            cadena = new Cadena { Nombre = nombre };
            _db.Cadenas.Add(cadena);
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Cadena guardada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Restaurante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarRestaurante(int? id, string nombre, int? cadenaId, bool activo = true)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length is < 2 or > 120)
            return BadRequest("El nombre debe tener entre 2 y 120 caracteres.");
        if (cadenaId.HasValue && !await _db.Cadenas.AnyAsync(c => c.Id == cadenaId.Value))
            return BadRequest("La cadena no existe.");

        Restaurante restaurante;
        var nuevo = !id.HasValue;
        if (nuevo)
        {
            restaurante = new Restaurante { Nombre = nombre, CadenaId = cadenaId, Activo = activo };
            _db.Restaurantes.Add(restaurante);
        }
        else
        {
            restaurante = await _db.Restaurantes.FindAsync(id!.Value) ?? null!;
            if (restaurante == null) return NotFound();
            restaurante.Nombre = nombre;
            restaurante.CadenaId = cadenaId;
            restaurante.Activo = activo;
        }
        await _db.SaveChangesAsync();
        if (nuevo)
            await _defaults.AsegurarEstacionesAsync(restaurante.Id);
        TempData["Ok"] = "Restaurante guardado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MiembroRestaurante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarMiembroRestaurante(
        int restauranteId, string email, RolRestaurante rol, bool activo)
    {
        if (!Enum.IsDefined(rol) || !await _db.Restaurantes.AnyAsync(r => r.Id == restauranteId))
            return BadRequest();
        var usuario = await _users.FindByEmailAsync((email ?? "").Trim());
        if (usuario == null) return BadRequest("El usuario debe existir previamente.");
        if (!usuario.Activo && activo) return BadRequest("No se puede activar una membresía de usuario deshabilitado.");

        var rolGlobal = rol switch
        {
            RolRestaurante.Administrador or RolRestaurante.Encargado => Roles.AdminRestaurante,
            RolRestaurante.Garzon => Roles.Garzon,
            RolRestaurante.Cocina => null, // Cocina/Bar requiere elección expresa de estación.
            _ => null
        };
        if (rolGlobal == null)
            return BadRequest("Seleccione un rol operativo compatible (administrador o garzón). Los operadores de estación se administran en el restaurante.");

        var membre = await _db.RestaurantesMiembros
            .FirstOrDefaultAsync(m => m.RestauranteId == restauranteId && m.UsuarioId == usuario.Id);
        if (membre == null)
        {
            membre = new RestauranteMiembro { RestauranteId = restauranteId, UsuarioId = usuario.Id };
            _db.RestaurantesMiembros.Add(membre);
        }
        var antesActivo = membre.Activo;
        membre.Rol = rol;
        membre.Activo = activo;
        var roleResult = await _users.AddToRoleAsync(usuario, rolGlobal);
        if (!roleResult.Succeeded && !await _users.IsInRoleAsync(usuario, rolGlobal))
            return BadRequest("No se pudo habilitar el rol global.");
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Membresía de restaurante guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MiembroCadena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarMiembroCadena(int cadenaId, string email, RolCadena rol)
    {
        if (!Enum.IsDefined(rol) || !await _db.Cadenas.AnyAsync(c => c.Id == cadenaId))
            return BadRequest();
        if (rol == RolCadena.Visualizador)
            return BadRequest("El rol visualizador no tiene una autorización de lectura global implementada.");
        var usuario = await _users.FindByEmailAsync((email ?? "").Trim());
        if (usuario == null || !usuario.Activo) return BadRequest("Usuario inexistente o inactivo.");
        var membresia = await _db.CadenasMiembros
            .FirstOrDefaultAsync(m => m.CadenaId == cadenaId && m.UsuarioId == usuario.Id);
        if (membresia == null)
        {
            membresia = new CadenaMiembro { CadenaId = cadenaId, UsuarioId = usuario.Id };
            _db.CadenasMiembros.Add(membresia);
        }
        membresia.Rol = rol;
        if (!await _users.IsInRoleAsync(usuario, Roles.AdminCadena))
        {
            var result = await _users.AddToRoleAsync(usuario, Roles.AdminCadena);
            if (!result.Succeeded) return BadRequest("No se pudo asignar el rol global.");
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Membresía de cadena guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("EliminarMiembroCadena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarMiembroCadena(int cadenaId, string usuarioId)
    {
        var miembro = await _db.CadenasMiembros
            .FirstOrDefaultAsync(m => m.CadenaId == cadenaId && m.UsuarioId == usuarioId);
        if (miembro == null) return NotFound();
        _db.CadenasMiembros.Remove(miembro);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Membresía de cadena revocada.";
        return RedirectToAction(nameof(Index));
    }
}
