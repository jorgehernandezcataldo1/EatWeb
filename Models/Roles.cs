using System.Security.Claims;

namespace EatWeb.Models;

public static class Roles
{
    public const string AdminCadena = "AdminCadena";
    public const string AdminRestaurante = "AdminRestaurante";
    public const string Garzon = "Garzon";

    // Listas separadas por coma SOLO para [Authorize(Roles = ...)]
    public const string Admin = AdminCadena + "," + AdminRestaurante;
    public const string AdminOGarzon = AdminCadena + "," + AdminRestaurante + "," + Garzon;

    // Helpers para usar en código (NO usar Roles.Admin aquí)
    public static bool EsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(AdminCadena) || user.IsInRole(AdminRestaurante);

    public static bool EsAdminCadena(this ClaimsPrincipal user) =>
        user.IsInRole(AdminCadena);

    public static bool EsAdminRestaurante(this ClaimsPrincipal user) =>
        user.IsInRole(AdminRestaurante);

    public static bool EsGarzon(this ClaimsPrincipal user) =>
        user.IsInRole(Garzon);

    /// <summary>Todos los roles en formato string (para chequeos manuales).</summary>
    public static readonly string[] TodosAdmin =
        { AdminCadena, AdminRestaurante };
}