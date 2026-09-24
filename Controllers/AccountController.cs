using EatWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EatWeb.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "Email y contraseña son requeridos");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.Activo)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas o usuario inactivo");
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName, password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Agregar claim con el RestauranteId
            var claims = new List<Claim>
            {
                new Claim("RestauranteId", user.RestauranteId.ToString())
            };

            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);

            // Redirigir según rol
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(Roles.Admin))
                return RedirectToAction("Index", "Admin");
            else if (roles.Contains(Roles.Garzon))
                return RedirectToAction("Index", "Mesas");

            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Intento de inicio de sesión inválido");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
