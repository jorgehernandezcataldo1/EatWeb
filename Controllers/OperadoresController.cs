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
public class OperadoresController : RestauranteControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OperadoresController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var membresias = await _context.RestaurantesMiembros
            .AsNoTracking()
            .Where(rm =>
                rm.RestauranteId == RestauranteId &&
                rm.Rol == RolRestaurante.Cocina)
            .Include(rm => rm.Usuario)
            .OrderBy(rm => rm.Usuario.NombreCompleto)
            .ToListAsync();

        var modelos = new List<OperadorEstacionViewModel>();

        foreach (var membresia in membresias)
        {
            var roles = await _userManager.GetRolesAsync(membresia.Usuario);
            var estacion = roles.Contains(Roles.Bar) ? Roles.Bar : Roles.Cocina;

            modelos.Add(new OperadorEstacionViewModel
            {
                Id = membresia.UsuarioId,
                NombreCompleto = membresia.Usuario.NombreCompleto,
                Email = membresia.Usuario.Email ?? string.Empty,
                Estacion = estacion,
                Activo = membresia.Activo
            });
        }

        return View(modelos);
    }

    [HttpGet]
    public IActionResult Crear() => View(new OperadorEstacionViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(OperadorEstacionViewModel modelo)
    {
        if (modelo.Estacion != Roles.Cocina && modelo.Estacion != Roles.Bar)
            ModelState.AddModelError(nameof(modelo.Estacion), "Estación inválida");

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

        var usuario = new ApplicationUser
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            NombreCompleto = modelo.NombreCompleto.Trim(),
            Activo = true
        };

        var creado = await _userManager.CreateAsync(usuario, modelo.Password!);
        if (!creado.Succeeded)
        {
            foreach (var error in creado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(modelo);
        }

        await _userManager.AddToRoleAsync(usuario, modelo.Estacion);

        _context.RestaurantesMiembros.Add(new RestauranteMiembro
        {
            RestauranteId = RestauranteId,
            UsuarioId = usuario.Id,
            Rol = RolRestaurante.Cocina,
            Activo = modelo.Activo
        });

        await _context.SaveChangesAsync();

        TempData["Ok"] = $"Operador de {modelo.Estacion} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(string id)
    {
        var membresia = await _context.RestaurantesMiembros
            .FirstOrDefaultAsync(rm =>
                rm.RestauranteId == RestauranteId &&
                rm.UsuarioId == id &&
                rm.Rol == RolRestaurante.Cocina);

        if (membresia == null)
            return NotFound();

        membresia.Activo = !membresia.Activo;
        await _context.SaveChangesAsync();

        TempData["Ok"] = membresia.Activo
            ? "Operador activado en este restaurante."
            : "Operador desactivado en este restaurante.";

        return RedirectToAction(nameof(Index));
    }
}
