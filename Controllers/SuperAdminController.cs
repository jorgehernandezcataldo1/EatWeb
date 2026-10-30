using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.Realtime;
using EatWeb.ViewModels;
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
    private readonly RealtimeAccessRevoker _revoker;

    public SuperAdminController(ApplicationDbContext db,
        UserManager<ApplicationUser> users, RestauranteDefaultsService defaults,
        RealtimeAccessRevoker revoker)
    {
        _db = db;
        _users = users;
        _defaults = defaults;
        _revoker = revoker;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var cadenas = await _db.Cadenas.AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new CadenaSuperAdminItem(c.Id, c.Nombre, c.Restaurantes.Count(), c.Activo))
            .ToListAsync();
        var restaurantes = await _db.Restaurantes.AsNoTracking()
            .OrderBy(r => r.Nombre)
            .Select(r => new RestauranteSuperAdminItem(r.Id, r.Nombre, r.CadenaId, r.Cadena != null ? r.Cadena.Nombre : "Independiente", r.Activo))
            .ToListAsync();
        var miembrosCadena = await _db.CadenasMiembros.AsNoTracking()
            .OrderBy(m => m.CadenaId)
            .Select(m => new MiembroCadenaSuperAdminItem(m.CadenaId, m.UsuarioId, m.Usuario.Email ?? "", m.Rol))
            .ToListAsync();
        var miembrosRestaurante = await _db.RestaurantesMiembros.AsNoTracking()
            .OrderBy(m => m.RestauranteId)
            .Select(m => new MiembroRestauranteSuperAdminItem(m.RestauranteId, m.UsuarioId, m.Usuario.Email ?? "", m.Rol, m.Activo))
            .ToListAsync();

        return View(new SuperAdminIndexViewModel
        {
            Cadenas = cadenas,
            Restaurantes = restaurantes,
            MiembrosCadena = miembrosCadena,
            MiembrosRestaurante = miembrosRestaurante
        });
    }

    [HttpPost("Cadena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCadena(int? id, string nombre, bool activo = true)
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
            cadena.Activo = activo;
        }
        else
        {
            cadena = new Cadena { Nombre = nombre, Activo = activo };
            _db.Cadenas.Add(cadena);
        }
        await _db.SaveChangesAsync();
        if (!activo)
        {
            var restaurants = await _db.Restaurantes.AsNoTracking()
                .Where(r => r.CadenaId == cadena.Id)
                .Select(r => new
                {
                    r.Id,
                    Miembros = r.Miembros.Select(m => m.UsuarioId).ToList(),
                    AdminsCadena = r.Cadena!.Miembros.Select(m => m.UsuarioId).ToList()
                })
                .ToListAsync();
            foreach (var restaurante in restaurants)
                foreach (var usuarioId in restaurante.Miembros.Concat(restaurante.AdminsCadena).Distinct())
                    await _revoker.RevokeRestaurantAsync(usuarioId, restaurante.Id);
        }
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
        if (!restaurante.Activo)
        {
            var miembros = await _db.RestaurantesMiembros.Where(m => m.RestauranteId == restaurante.Id)
                .Select(m => m.UsuarioId).ToListAsync();
            var admins = restaurante.CadenaId.HasValue
                ? await _db.CadenasMiembros.Where(m => m.CadenaId == restaurante.CadenaId.Value)
                    .Select(m => m.UsuarioId).ToListAsync()
                : new List<string>();
            foreach (var usuarioId in miembros.Concat(admins).Distinct())
                await _revoker.RevokeRestaurantAsync(usuarioId, restaurante.Id);
        }
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
        var rolAnterior = membre.Rol;
        membre.Rol = rol;
        membre.Activo = activo;
        var roleResult = await _users.AddToRoleAsync(usuario, rolGlobal);
        if (!roleResult.Succeeded && !await _users.IsInRoleAsync(usuario, rolGlobal))
            return BadRequest("No se pudo habilitar el rol global.");
        await _db.SaveChangesAsync();
        if (!activo || (antesActivo && rolAnterior != rol))
            await _revoker.RevokeRestaurantAsync(usuario.Id, restauranteId);
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
        foreach (var restauranteId in await _db.Restaurantes.AsNoTracking()
            .Where(r => r.CadenaId == cadenaId).Select(r => r.Id).ToListAsync())
            await _revoker.RevokeRestaurantAsync(usuarioId, restauranteId);
        TempData["Ok"] = "Membresía de cadena revocada.";
        return RedirectToAction(nameof(Index));
    }
}
