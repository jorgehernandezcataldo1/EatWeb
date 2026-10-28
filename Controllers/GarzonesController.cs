using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class GarzonesController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GarzonesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var restauranteId = RestauranteId;

        var garzones = await _context.RestaurantesMiembros
            .AsNoTracking()
            .Where(rm =>
                rm.RestauranteId == restauranteId &&
                rm.Rol == RolRestaurante.Garzon)
            .OrderBy(rm => rm.Usuario.NombreCompleto)
            .Select(rm => new GarzonViewModel
            {
                Id = rm.UsuarioId,
                NombreCompleto = rm.Usuario.NombreCompleto,
                Email = rm.Usuario.Email ?? "",
                Activo = rm.Activo,
                Mesas = rm.Usuario.Mesas
                    .Where(m => m.RestauranteId == restauranteId)
                    .Select(m => m.Numero)
                    .OrderBy(n => n)
                    .ToList()
            })
            .ToListAsync();

        return View(garzones);
    }

    [HttpGet]
    public IActionResult Crear() => View(new GarzonViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(GarzonViewModel modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo.Password))
            ModelState.AddModelError(nameof(modelo.Password), "La contraseña es requerida");

        if (!ModelState.IsValid)
            return View(modelo);

        if (await _userManager.FindByEmailAsync(modelo.Email) != null)
        {
            ModelState.AddModelError(nameof(modelo.Email),
                "Ya existe una cuenta con ese email. Por seguridad no se vincula automáticamente a otro restaurante.");
            return View(modelo);
        }

        var garzon = new ApplicationUser
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            NombreCompleto = modelo.NombreCompleto.Trim(),
            Activo = true
        };

        var resultado = await _userManager.CreateAsync(garzon, modelo.Password!);

        if (!resultado.Succeeded)
        {
            AgregarErrores(resultado);
            return View(modelo);
        }

        await _userManager.AddToRoleAsync(garzon, Roles.Garzon);

        _context.RestaurantesMiembros.Add(new RestauranteMiembro
        {
            RestauranteId = RestauranteId,
            UsuarioId = garzon.Id,
            Rol = RolRestaurante.Garzon,
            Activo = modelo.Activo
        });

        await _context.SaveChangesAsync();

        TempData["Ok"] = $"Garzón {garzon.NombreCompleto} creado";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string id)
    {
        var membresia = await BuscarMembresiaGarzonAsync(id);
        if (membresia == null)
            return NotFound();

        return View(new GarzonViewModel
        {
            Id = membresia.UsuarioId,
            NombreCompleto = membresia.Usuario.NombreCompleto,
            Email = membresia.Usuario.Email ?? "",
            Activo = membresia.Activo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(string id, GarzonViewModel modelo)
    {
        if (id != modelo.Id)
            return BadRequest();

        var membresia = await BuscarMembresiaGarzonAsync(id);
        if (membresia == null)
            return NotFound();

        var garzon = membresia.Usuario;
        var esCompartido =
            await _context.RestaurantesMiembros.CountAsync(rm => rm.UsuarioId == id) > 1 ||
            await _context.CadenasMiembros.AnyAsync(cm => cm.UsuarioId == id);

        var cambiaEmail = !string.Equals(garzon.Email, modelo.Email, StringComparison.OrdinalIgnoreCase);
        var cambiaNombre = !string.Equals(
            garzon.NombreCompleto?.Trim(),
            modelo.NombreCompleto?.Trim(),
            StringComparison.Ordinal);

        if (esCompartido &&
            (cambiaEmail || cambiaNombre || !string.IsNullOrWhiteSpace(modelo.Password)))
        {
            ModelState.AddModelError(string.Empty,
                "Esta cuenta pertenece a más de un restaurante. Desde este local solo puedes activar o desactivar su membresía; los datos de identidad se administran de forma global.");
        }

        if (cambiaEmail && !esCompartido)
        {
            var otro = await _userManager.FindByEmailAsync(modelo.Email);
            if (otro != null && otro.Id != garzon.Id)
                ModelState.AddModelError(nameof(modelo.Email), "Ya existe un usuario con ese email");
        }

        if (!ModelState.IsValid)
        {
            modelo.Activo = membresia.Activo;
            return View(modelo);
        }

        if (!esCompartido && !string.IsNullOrWhiteSpace(modelo.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(garzon);
            var reset = await _userManager.ResetPasswordAsync(garzon, token, modelo.Password);
            if (!reset.Succeeded)
            {
                AgregarErrores(reset);
                modelo.Activo = membresia.Activo;
                return View(modelo);
            }
        }

        if (!esCompartido)
        {
            if (cambiaEmail)
            {
                var emailResult = await _userManager.SetEmailAsync(garzon, modelo.Email);
                if (!emailResult.Succeeded)
                {
                    AgregarErrores(emailResult);
                    return View(modelo);
                }

                var userNameResult = await _userManager.SetUserNameAsync(garzon, modelo.Email);
                if (!userNameResult.Succeeded)
                {
                    AgregarErrores(userNameResult);
                    return View(modelo);
                }
            }

            garzon.NombreCompleto = modelo.NombreCompleto.Trim();
            garzon.Activo = true;

            var actualizado = await _userManager.UpdateAsync(garzon);
            if (!actualizado.Succeeded)
            {
                AgregarErrores(actualizado);
                return View(modelo);
            }
        }

        var estabaActivo = membresia.Activo;
        membresia.Activo = modelo.Activo;
        await _context.SaveChangesAsync();

        if (estabaActivo && !membresia.Activo)
            await LiberarMesasLocalesAsync(garzon.Id);

        TempData["Ok"] = "Garzón actualizado";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(string id)
    {
        var membresia = await BuscarMembresiaGarzonAsync(id);
        if (membresia == null)
            return NotFound();

        membresia.Activo = !membresia.Activo;
        await _context.SaveChangesAsync();

        if (membresia.Activo)
        {
            TempData["Ok"] = "Garzón activado en este restaurante";
        }
        else
        {
            var liberadas = await LiberarMesasLocalesAsync(membresia.UsuarioId);
            TempData["Ok"] = $"Garzón desactivado en este restaurante. Mesas liberadas: {liberadas}";
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<RestauranteMiembro?> BuscarMembresiaGarzonAsync(string id)
    {
        return _context.RestaurantesMiembros
            .Include(rm => rm.Usuario)
            .FirstOrDefaultAsync(rm =>
                rm.UsuarioId == id &&
                rm.RestauranteId == RestauranteId &&
                rm.Rol == RolRestaurante.Garzon);
    }

    private Task<int> LiberarMesasLocalesAsync(string usuarioId)
    {
        var restauranteId = RestauranteId;

        return _context.Mesas
            .Where(m =>
                m.RestauranteId == restauranteId &&
                m.GarzonId == usuarioId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(m => m.GarzonId, (string?)null));
    }

    private void AgregarErrores(IdentityResult resultado)
    {
        foreach (var e in resultado.Errors)
        {
            var mensaje = e.Code switch
            {
                "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres",
                "PasswordRequiresDigit" => "La contraseña debe incluir un número",
                "PasswordRequiresLower" => "La contraseña debe incluir una minúscula",
                "PasswordRequiresUpper" => "La contraseña debe incluir una mayúscula",
                "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir un símbolo (ej: !)",
                "DuplicateUserName" or "DuplicateEmail" => "Ya existe un usuario con ese email",
                _ => e.Description
            };

            ModelState.AddModelError(string.Empty, mensaje);
        }
    }
}
