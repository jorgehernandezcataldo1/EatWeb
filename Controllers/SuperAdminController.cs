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
        var restaurantes = await _db.Restaurantes.AsNoTracking().OrderBy(r => r.Nombre)
            .Select(r => new RestauranteSuperAdminItem(r.Id, r.Nombre, r.CadenaId,
                r.Cadena != null ? r.Cadena.Nombre : "Independiente", r.Activo))
            .ToListAsync();
        ViewBag.Planes = await _db.PlanesComerciales.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
        ViewBag.Funcionalidades = await _db.Funcionalidades.AsNoTracking().OrderBy(f => f.Clave).ToListAsync();
        ViewBag.PlanFuncionalidades = await _db.PlanesFuncionalidades.AsNoTracking()
            .Select(x => new { x.PlanComercialId, x.FuncionalidadId })
            .ToListAsync();
        ViewBag.Suscripciones = await _db.SuscripcionesComerciales.AsNoTracking().ToListAsync();
        ViewBag.UsuariosPorRestaurante = await _db.RestaurantesMiembros.AsNoTracking()
            .Where(m => m.Activo && m.Usuario.Activo)
            .GroupBy(m => m.RestauranteId)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Total);
        return View(new SuperAdminIndexViewModel
        {
            Cadenas = cadenas,
            Restaurantes = restaurantes
        });
    }


    [HttpGet("Restaurante/{id:int}")]
    public async Task<IActionResult> DetalleRestaurante(int id)
    {
        var restaurante = await _db.Restaurantes.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Id, r.Nombre, r.CadenaId, r.Activo })
            .FirstOrDefaultAsync();
        if (restaurante == null) return NotFound();

        var miembros = await _db.RestaurantesMiembros.AsNoTracking()
            .Where(m => m.RestauranteId == id)
            .OrderBy(m => m.Usuario.NombreCompleto)
            .Select(m => new MiembroRestauranteSuperAdminItem(
                m.RestauranteId, m.UsuarioId, m.Usuario.Email ?? "", m.Rol, m.Activo))
            .ToListAsync();
        var propio = await _db.SuscripcionesComerciales.AsNoTracking()
            .Where(s => s.RestauranteId == id).Select(s => (int?)s.PlanComercialId)
            .FirstOrDefaultAsync();
        var heredado = restaurante.CadenaId == null ? null :
            await _db.SuscripcionesComerciales.AsNoTracking()
                .Where(s => s.CadenaId == restaurante.CadenaId)
                .Select(s => (int?)s.PlanComercialId).FirstOrDefaultAsync();
        return View(new SuperAdminRestauranteDetalleViewModel
        {
            RestauranteId = restaurante.Id,
            Nombre = restaurante.Nombre,
            CadenaId = restaurante.CadenaId,
            Activo = restaurante.Activo,
            Miembros = miembros,
            Funcionalidades = await _db.Funcionalidades.AsNoTracking().OrderBy(f => f.Clave).ToListAsync(),
            Excepciones = await _db.RestaurantesFuncionalidadesOverride.AsNoTracking()
                .Where(o => o.RestauranteId == id)
                .ToDictionaryAsync(o => o.FuncionalidadId, o => o.Habilitada),
            Planes = await _db.PlanesComerciales.AsNoTracking().ToListAsync(),
            PlanPropioId = propio,
            PlanCadenaId = heredado
        });
    }

    [HttpPost("Plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPlan(int? id, string nombre, int? maxRestaurantes, bool activo = true)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length is < 2 or > 120 || maxRestaurantes is <= 0)
            return BadRequest("Nombre o límite inválido.");
        var plan = id.HasValue
            ? await _db.PlanesComerciales.FindAsync(id.Value)
            : new PlanComercial();
        if (plan == null) return NotFound();
        plan.Nombre = nombre;
        plan.MaxRestaurantes = maxRestaurantes;
        plan.Activo = activo;
        if (!id.HasValue) _db.PlanesComerciales.Add(plan);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Funcionalidad")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearFuncionalidad(string clave, string nombre)
    {
        clave = (clave ?? "").Trim().ToLowerInvariant();
        nombre = (nombre ?? "").Trim();
        if (clave.Length is < 3 or > 80 || nombre.Length is < 2 or > 120 ||
            clave.Any(c => !char.IsAsciiLetterLower(c) && !char.IsDigit(c) && c != '.' && c != '_'))
            return BadRequest("Clave o nombre inválido.");
        if (await _db.Funcionalidades.AnyAsync(f => f.Clave == clave))
            return BadRequest("La funcionalidad ya existe.");
        _db.Funcionalidades.Add(new Funcionalidad { Clave = clave, Nombre = nombre });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("PlanFuncionalidad")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DefinirPlanFuncionalidad(int planId, int funcionalidadId, bool habilitada)
    {
        if (!await _db.PlanesComerciales.AnyAsync(p => p.Id == planId) ||
            !await _db.Funcionalidades.AnyAsync(f => f.Id == funcionalidadId))
            return NotFound();
        var relacion = await _db.PlanesFuncionalidades.FindAsync(planId, funcionalidadId);
        if (habilitada && relacion == null)
            _db.PlanesFuncionalidades.Add(new PlanFuncionalidad
            {
                PlanComercialId = planId,
                FuncionalidadId = funcionalidadId
            });
        if (!habilitada && relacion != null)
            _db.PlanesFuncionalidades.Remove(relacion);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Suscripcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSuscripcion(
        int? cadenaId, int? restauranteId, int planId, bool activa = true,
        DateTime? venceUtc = null)
    {
        if (cadenaId.HasValue == restauranteId.HasValue)
            return BadRequest("Selecciona una cadena o un restaurante, no ambos.");
        var plan = await _db.PlanesComerciales.FindAsync(planId);
        if (plan == null || !plan.Activo) return BadRequest("El plan no está disponible.");
        if (venceUtc.HasValue && venceUtc.Value.Kind != DateTimeKind.Utc)
            venceUtc = DateTime.SpecifyKind(venceUtc.Value, DateTimeKind.Utc);
        if (cadenaId.HasValue &&
            !await _db.Cadenas.AnyAsync(c => c.Id == cadenaId.Value))
            return NotFound();
        if (restauranteId.HasValue &&
            !await _db.Restaurantes.AnyAsync(r => r.Id == restauranteId.Value))
            return NotFound();

        if (cadenaId.HasValue && plan.MaxRestaurantes.HasValue)
        {
            var total = await _db.Restaurantes.CountAsync(r => r.CadenaId == cadenaId.Value);
            if (total > plan.MaxRestaurantes.Value)
                return BadRequest("La cadena supera el máximo de restaurantes del plan.");
        }
        var suscripcion = await _db.SuscripcionesComerciales
            .FirstOrDefaultAsync(s => cadenaId.HasValue
                ? s.CadenaId == cadenaId.Value
                : s.RestauranteId == restauranteId!.Value);
        if (suscripcion == null)
        {
            suscripcion = new SuscripcionComercial
            {
                CadenaId = cadenaId,
                RestauranteId = restauranteId
            };
            _db.SuscripcionesComerciales.Add(suscripcion);
        }
        suscripcion.PlanComercialId = planId;
        suscripcion.Activa = activa;
        suscripcion.VenceUtc = venceUtc;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("EliminarSuscripcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarSuscripcion(int? cadenaId, int? restauranteId)
    {
        if (cadenaId.HasValue == restauranteId.HasValue)
            return BadRequest("Indica una cadena o un restaurante.");
        var suscripcion = await _db.SuscripcionesComerciales
            .FirstOrDefaultAsync(s => cadenaId.HasValue
                ? s.CadenaId == cadenaId.Value
                : s.RestauranteId == restauranteId!.Value);
        if (suscripcion == null) return NotFound();
        _db.SuscripcionesComerciales.Remove(suscripcion);
        await _db.SaveChangesAsync();
        return restauranteId.HasValue
            ? RedirectToAction(nameof(DetalleRestaurante), new { id = restauranteId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("ExcepcionRestaurante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarExcepcionRestaurante(
        int restauranteId, int funcionalidadId, string modo)
    {
        if (!await _db.Restaurantes.AnyAsync(r => r.Id == restauranteId) ||
            !await _db.Funcionalidades.AnyAsync(f => f.Id == funcionalidadId))
            return NotFound();
        if (modo is not ("heredar" or "permitir" or "bloquear"))
            return BadRequest("Modo inválido.");
        var existente = await _db.RestaurantesFuncionalidadesOverride
            .FindAsync(restauranteId, funcionalidadId);
        if (modo == "heredar")
        {
            if (existente != null) _db.RestaurantesFuncionalidadesOverride.Remove(existente);
        }
        else
        {
            if (existente == null)
            {
                existente = new RestauranteFuncionalidadOverride
                {
                    RestauranteId = restauranteId,
                    FuncionalidadId = funcionalidadId
                };
                _db.RestaurantesFuncionalidadesOverride.Add(existente);
            }
            existente.Habilitada = modo == "permitir";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(DetalleRestaurante), new { id = restauranteId });
    }

    [HttpPost("Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearUsuario(string email, string nombre, string password)
    {
        email = (email ?? "").Trim();
        nombre = (nombre ?? "").Trim();
        if (email.Length is < 5 or > 254 || nombre.Length is < 2 or > 100 ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var direccion) ||
            !string.Equals(direccion.Address, email, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Correo o nombre inválido.");
        if (await _users.FindByEmailAsync(email) != null)
            return BadRequest("El correo ya está registrado.");

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NombreCompleto = nombre,
            Activo = true
        };
        var result = await _users.CreateAsync(usuario, password ?? "");
        if (!result.Succeeded)
            return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));
        TempData["Ok"] = "Usuario creado. Asigna su membresía desde el panel.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("EstadoUsuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EstadoUsuario(string usuarioId, bool activo)
    {
        var miId = _users.GetUserId(User);
        if (usuarioId == miId && !activo)
            return BadRequest("No puedes desactivar tu propia cuenta SuperAdmin.");

        var usuario = await _users.FindByIdAsync(usuarioId);
        if (usuario == null) return NotFound();
        usuario.Activo = activo;
        var actualizado = await _users.UpdateAsync(usuario);
        if (!actualizado.Succeeded) return BadRequest("No se pudo modificar el usuario.");

        var stamp = await _users.UpdateSecurityStampAsync(usuario);
        if (!stamp.Succeeded) return BadRequest("No se pudo invalidar la sesión.");

        if (!activo)
        {
            var locales = await _db.RestaurantesMiembros.AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId)
                .Select(m => m.RestauranteId).ToListAsync();
            var cadenas = await _db.CadenasMiembros.AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId)
                .Select(m => m.CadenaId).ToListAsync();
            var localesCadena = await _db.Restaurantes.AsNoTracking()
                .Where(r => r.CadenaId != null && cadenas.Contains(r.CadenaId.Value))
                .Select(r => r.Id).ToListAsync();
            foreach (var restauranteId in locales.Concat(localesCadena).Distinct())
                await _revoker.RevokeRestaurantAsync(usuarioId, restauranteId);
        }

        TempData["Ok"] = activo ? "Usuario activado." : "Usuario desactivado y sesiones invalidadas.";
        return RedirectToAction(nameof(Index));
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

        // Un plan de cadena puede limitar el número de sucursales.
        // Al crear o trasladar un local, respetar el límite contratado.
        var restauranteAnterior = id.HasValue
            ? await _db.Restaurantes.AsNoTracking()
                .Where(r => r.Id == id.Value)
                .Select(r => new { r.CadenaId })
                .FirstOrDefaultAsync()
            : null;
        if (id.HasValue && restauranteAnterior == null)
            return NotFound();

        if (cadenaId.HasValue && (!id.HasValue || restauranteAnterior!.CadenaId != cadenaId))
        {
            var max = await _db.SuscripcionesComerciales.AsNoTracking()
                .Where(s => s.CadenaId == cadenaId)
                .Select(s => s.PlanComercial.MaxRestaurantes)
                .FirstOrDefaultAsync();
            if (max.HasValue &&
                await _db.Restaurantes.CountAsync(r => r.CadenaId == cadenaId) >= max.Value)
                return BadRequest("La cadena alcanzó el máximo de restaurantes de su plan.");
        }

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
        int restauranteId, string email, RolRestaurante rol, bool activo, string? estacion = null)
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
            RolRestaurante.Cocina => estacion switch
            {
                Roles.Bar => Roles.Bar,
                Roles.Cocina => Roles.Cocina,
                null or "" => await _users.IsInRoleAsync(usuario, Roles.Bar)
                    ? Roles.Bar : Roles.Cocina,
                _ => null
            },
            _ => null
        };
        if (rolGlobal == null)
            return BadRequest("Rol de membresía no habilitado para operaciones.");

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
        if (!await _users.IsInRoleAsync(usuario, rolGlobal))
        {
            var roleResult = await _users.AddToRoleAsync(usuario, rolGlobal);
            if (!roleResult.Succeeded)
                return BadRequest("No se pudo habilitar el rol global.");
        }
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
