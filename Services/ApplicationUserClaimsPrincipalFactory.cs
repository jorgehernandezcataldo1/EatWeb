using EatWeb.Data;
using EatWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EatWeb.Services;

public class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ApplicationDbContext _context;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        ApplicationDbContext context)
        : base(userManager, roleManager, options)
    {
        _context = context;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var restaurante = await _context.RestaurantesMiembros
            .AsNoTracking()
            .Where(rm => rm.UsuarioId == user.Id)
            .Select(rm => new
            {
                rm.RestauranteId,
                RestauranteNombre = rm.Restaurante.Nombre,
                rm.Restaurante.CadenaId
            })
            .FirstOrDefaultAsync();

        if (restaurante != null)
        {
            identity.AddClaim(
                new Claim(
                    "RestauranteId",
                    restaurante.RestauranteId.ToString()));

            identity.AddClaim(
                new Claim(
                    "RestauranteNombre",
                    restaurante.RestauranteNombre));

            if (restaurante.CadenaId.HasValue)
            {
                identity.AddClaim(
                    new Claim(
                        "CadenaId",
                        restaurante.CadenaId.Value.ToString()));
            }
        }

        return identity;
    }
}
