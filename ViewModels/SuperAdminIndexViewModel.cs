using EatWeb.Models.Enums;

namespace EatWeb.ViewModels;

public sealed class SuperAdminIndexViewModel
{
    public List<CadenaSuperAdminItem> Cadenas { get; set; } = new();
    public List<RestauranteSuperAdminItem> Restaurantes { get; set; } = new();
    public List<MiembroCadenaSuperAdminItem> MiembrosCadena { get; set; } = new();
    public List<MiembroRestauranteSuperAdminItem> MiembrosRestaurante { get; set; } = new();
}
public sealed record CadenaSuperAdminItem(int Id, string Nombre, int Restaurantes, bool Activo);
public sealed record RestauranteSuperAdminItem(int Id, string Nombre, int? CadenaId, string Cadena, bool Activo);
public sealed record MiembroCadenaSuperAdminItem(int CadenaId, string UsuarioId, string Email, RolCadena Rol);
public sealed record MiembroRestauranteSuperAdminItem(int RestauranteId, string UsuarioId, string Email, RolRestaurante Rol, bool Activo);
