namespace EatWeb.Models;

// Catálogo de capacidades comerciales, independiente de los roles de Identity.
public class Funcionalidad
{
    public int Id { get; set; }
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; } = true;
}
public class PlanComercial
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public int? MaxRestaurantes { get; set; }
    public ICollection<PlanFuncionalidad> Funcionalidades { get; set; } = new List<PlanFuncionalidad>();
}
public class PlanFuncionalidad
{
    public int PlanComercialId { get; set; }
    public PlanComercial PlanComercial { get; set; } = null!;
    public int FuncionalidadId { get; set; }
    public Funcionalidad Funcionalidad { get; set; } = null!;
}
public class SuscripcionComercial
{
    public int Id { get; set; }
    public int PlanComercialId { get; set; }
    public PlanComercial PlanComercial { get; set; } = null!;
    public int? CadenaId { get; set; }
    public Cadena? Cadena { get; set; }
    public int? RestauranteId { get; set; }
    public Restaurante? Restaurante { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime? VenceUtc { get; set; }
}
public class RestauranteFuncionalidadOverride
{
    public int RestauranteId { get; set; }
    public Restaurante Restaurante { get; set; } = null!;
    public int FuncionalidadId { get; set; }
    public Funcionalidad Funcionalidad { get; set; } = null!;
    public bool Habilitada { get; set; }
}
