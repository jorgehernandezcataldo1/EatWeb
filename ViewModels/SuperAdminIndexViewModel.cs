using EatWeb.Models.Enums;

namespace EatWeb.ViewModels;

public sealed class SuperAdminIndexViewModel
{
    public List<UsuarioSuperAdminItem> Usuarios { get; set; } = new();
    public List<CadenaSuperAdminItem> Cadenas { get; set; } = new();
    public List<RestauranteSuperAdminItem> Restaurantes { get; set; } = new();
    public List<MiembroCadenaSuperAdminItem> MiembrosCadena { get; set; } = new();
    public List<MiembroRestauranteSuperAdminItem> MiembrosRestaurante { get; set; } = new();
}
public sealed record CadenaSuperAdminItem(int Id, string Nombre, int Restaurantes, bool Activo);
public sealed record RestauranteSuperAdminItem(int Id, string Nombre, int? CadenaId, string Cadena, bool Activo);
public sealed record MiembroCadenaSuperAdminItem(int CadenaId, string UsuarioId, string Email, RolCadena Rol);
public sealed record MiembroRestauranteSuperAdminItem(int RestauranteId, string UsuarioId, string Email, RolRestaurante Rol, bool Activo);

public sealed record UsuarioSuperAdminItem(string Id, string Email, string Nombre, bool Activo);

public sealed class SuperAdminRestauranteDetalleViewModel
{
    public int RestauranteId { get; set; }
    public string Nombre { get; set; } = "";
    public int? CadenaId { get; set; }
    public bool Activo { get; set; }
    public List<MiembroRestauranteSuperAdminItem> Miembros { get; set; } = new();
    public List<EatWeb.Models.Funcionalidad> Funcionalidades { get; set; } = new();
    public Dictionary<int, bool> Excepciones { get; set; } = new();
    public List<EatWeb.Models.PlanComercial> Planes { get; set; } = new();
    public int? PlanPropioId { get; set; }
    public int? PlanCadenaId { get; set; }
}
