using EatWeb.Data;
using EatWeb.Models;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.Admin)]
public class GarzonesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GarzonesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private int GetRestauranteId() =>
        int.Parse(User.FindFirst("RestauranteId")?.Value ?? "0");

    public async Task<IActionResult> Index()
    {
        var garzones = await GarzonesDelRestaurante(GetRestauranteId())
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new GarzonViewModel
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email ?? "",
                Activo = u.Activo,
                Mesas = u.Mesas.Select(m => m.Numero).OrderBy(n => n).ToList()
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
            ModelState.AddModelError(nameof(modelo.Email), "Ya existe un usuario con ese email");
            return View(modelo);
        }

        var garzon = new ApplicationUser
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            NombreCompleto = modelo.NombreCompleto.Trim(),
            RestauranteId = GetRestauranteId(),
            Activo = modelo.Activo
        };

        var resultado = await _userManager.CreateAsync(garzon, modelo.Password!);
        if (!resultado.Succeeded)
        {
            AgregarErrores(resultado);
            return View(modelo);
        }

        await _userManager.AddToRoleAsync(garzon, Roles.Garzon);

        TempData["Ok"] = $"Garzón {garzon.NombreCompleto} creado";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string id)
    {
        var garzon = await BuscarGarzonAsync(id);
        if (garzon == null) return NotFound();

        return View(new GarzonViewModel
        {
            Id = garzon.Id,
            NombreCompleto = garzon.NombreCompleto,
            Email = garzon.Email ?? "",
            Activo = garzon.Activo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(string id, GarzonViewModel modelo)
    {
        if (id != modelo.Id) return BadRequest();
        if (!ModelState.IsValid) return View(modelo);

        var garzon = await BuscarGarzonAsync(id);
        if (garzon == null) return NotFound();

        var cambiaEmail = !string.Equals(garzon.Email, modelo.Email, StringComparison.OrdinalIgnoreCase);
        if (cambiaEmail)
        {
            var otro = await _userManager.FindByEmailAsync(modelo.Email);
            if (otro != null && otro.Id != garzon.Id)
            {
                ModelState.AddModelError(nameof(modelo.Email), "Ya existe un usuario con ese email");
                return View(modelo);
            }
        }

        // 1) Contraseña primero: si falla la política, no se cambia nada más
        if (!string.IsNullOrWhiteSpace(modelo.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(garzon);
            var reset = await _userManager.ResetPasswordAsync(garzon, token, modelo.Password);
            if (!reset.Succeeded)
            {
                AgregarErrores(reset);
                return View(modelo);
            }
        }

        // 2) Resto de datos
        if (cambiaEmail)
        {
            await _userManager.SetEmailAsync(garzon, modelo.Email);
            await _userManager.SetUserNameAsync(garzon, modelo.Email); // el login usa UserName
        }

        garzon.NombreCompleto = modelo.NombreCompleto.Trim();
        garzon.Activo = modelo.Activo;

        var actualizado = await _userManager.UpdateAsync(garzon);
        if (!actualizado.Succeeded)
        {
            AgregarErrores(actualizado);
            return View(modelo);
        }

        TempData["Ok"] = "Garzón actualizado";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(string id)
    {
        var garzon = await BuscarGarzonAsync(id);
        if (garzon == null) return NotFound();

        garzon.Activo = !garzon.Activo;
        await _userManager.UpdateAsync(garzon);

        if (garzon.Activo)
        {
            TempData["Ok"] = "Garzón activado";
        }
        else
        {
            // Invalida su sesión abierta y libera sus mesas
            await _userManager.UpdateSecurityStampAsync(garzon);
            var liberadas = await _context.Mesas
                .Where(m => m.GarzonId == garzon.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.GarzonId, (string?)null));

            TempData["Ok"] = $"Garzón desactivado. Mesas liberadas: {liberadas}";
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------
    private IQueryable<ApplicationUser> GarzonesDelRestaurante(int restauranteId) =>
        _context.Users.Where(u =>
            u.RestauranteId == restauranteId &&
            _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Garzon)));

    /// <summary>Solo devuelve usuarios GARZÓN de MI restaurante (un admin no se edita aquí).</summary>
    private async Task<ApplicationUser?> BuscarGarzonAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.RestauranteId != GetRestauranteId()) return null;
        if (!await _userManager.IsInRoleAsync(user, Roles.Garzon)) return null;
        return user;
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