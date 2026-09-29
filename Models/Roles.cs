using System.Security.Claims;

namespace EatWeb.Models;

public static class Roles
{
    public const string AdminCadena = "AdminCadena";
    public const string AdminRestaurante = "AdminRestaurante";
    public const string Garzon = "Garzon";

    public const string Admin =
        AdminCadena + "," + AdminRestaurante;

    public const string AdminOGarzon =
        AdminCadena + "," + AdminRestaurante + "," + Garzon;
    public static bool EsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(AdminCadena) || user.IsInRole(AdminRestaurante);
}
