using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using EatWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EatWeb.Controllers;

[Authorize(Roles = Roles.AdminCadena)]
public class CadenasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestauranteDefaultsService _defaults;

    public CadenasController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RestauranteDefaultsService defaults)
    {
        _context = context;
        _userManager = userManager;
        _defaults = defaults;
    }

    private string UsuarioId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IQueryable<CadenaMiembro> MembresiasAdministrables =>
        _context.CadenasMiembros.Where(cm =>
            cm.UsuarioId == UsuarioId &&
            (cm.Rol == RolCadena.Administrador || cm.Rol == RolCadena.Gerente));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cadenaId = await MembresiasAdministrables
            .Select(cm => (int?)cm.CadenaId)
            .FirstOrDefaultAsync();

        if (cadenaId == null)
            return Forbid();

        var restaurantes = await _context.Restaurantes
            .AsNoTracking()
            .Where(r => r.CadenaId == cadenaId.Value)
            .OrderBy(r => r.Nombre)
            .Select(r => new RestauranteCadenaViewModel
            {
                Id = r.Id,
                Nombre = r.Nombre,
                Activo = r.Activo,
                TotalMesas = r.Mesas.Count(),
                TotalProductos = r.Productos.Count(),
                Admins = r.Miembros
                    .Where(m =>
                        m.Activo &&
                        (m.Rol == RolRestaurante.Administrador || m.Rol == RolRestaurante.Encargado))
                    .Select(m => m.Usuario.Email!)
                    .ToList()
            })
            .ToListAsync();

        return View(restaurantes);
    }

    [HttpGet]
    public IActionResult Crear() => View(new CrearRestauranteViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearRestauranteViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var cadenaId = await MembresiasAdministrables
            .Select(cm => (int?)cm.CadenaId)
            .FirstOrDefaultAsync();

        if (cadenaId == null)
            return Forbid();

        var adminExistente = await _userManager.FindByEmailAsync(modelo.AdminEmail);
        if (adminExistente != null)
        {
            ModelState.AddModelError(
                nameof(modelo.AdminEmail),
                "Ese correo ya pertenece a una cuenta. Por seguridad no se vincula automáticamente; usa otro correo mientras se implementa el flujo de invitaciones.");
            return View(modelo);
        }

        if (string.IsNullOrWhiteSpace(modelo.AdminPassword))
        {
            ModelState.AddModelError(
                nameof(modelo.AdminPassword),
                "La contraseña es requerida para un administrador nuevo.");
            return View(modelo);
        }

        var adminUser = new ApplicationUser
        {
            UserName = modelo.AdminEmail,
            Email = modelo.AdminEmail,
            NombreCompleto = modelo.AdminNombre,
            Activo = true
        };

        var result = await _userManager.CreateAsync(adminUser, modelo.AdminPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);

            return View(modelo);
        }

        if (!await _userManager.IsInRoleAsync(adminUser, Roles.AdminRestaurante))
            await _userManager.AddToRoleAsync(adminUser, Roles.AdminRestaurante);

        var restaurante = new Restaurante
        {
            Nombre = modelo.Nombre,
            CadenaId = cadenaId.Value,
            Activo = true
        };

        _context.Restaurantes.Add(restaurante);
        await _context.SaveChangesAsync();

        _context.RestaurantesMiembros.Add(new RestauranteMiembro
        {
            RestauranteId = restaurante.Id,
            UsuarioId = adminUser.Id,
            Rol = RolRestaurante.Administrador,
            Activo = true
        });

        await _context.SaveChangesAsync();
        await _defaults.AsegurarEstacionesAsync(restaurante.Id);

        TempData["Ok"] = $"Restaurante {restaurante.Nombre} creado";
        return RedirectToAction(nameof(Index));
    }
}
