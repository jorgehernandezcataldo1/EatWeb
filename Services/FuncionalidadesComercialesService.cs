using EatWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public sealed class FuncionalidadesComercialesService
{
    private readonly ApplicationDbContext _db;
    public FuncionalidadesComercialesService(ApplicationDbContext db) => _db = db;

    // La ausencia de suscripción no concede capacidades. Sin embargo, durante
    // el piloto se debe habilitar el enforcement módulo por módulo.
    public async Task<bool> HabilitadaAsync(int restauranteId, string clave, DateTime? fechaUtc = null)
    {
        var fecha = fechaUtc ?? DateTime.UtcNow;
        var local = await _db.Restaurantes.AsNoTracking()
            .Where(r => r.Id == restauranteId && r.Activo &&
                (r.CadenaId == null || r.Cadena!.Activo))
            .Select(r => new { r.CadenaId })
            .FirstOrDefaultAsync();
        if (local == null) return false;

        var feature = await _db.Funcionalidades.AsNoTracking()
            .Where(f => f.Clave == clave && f.Activa).Select(f => (int?)f.Id)
            .FirstOrDefaultAsync();
        if (!feature.HasValue) return false;

        var localOverride = await _db.RestaurantesFuncionalidadesOverride.AsNoTracking()
            .Where(o => o.RestauranteId == restauranteId && o.FuncionalidadId == feature.Value)
            .Select(o => (bool?)o.Habilitada).FirstOrDefaultAsync();
        if (localOverride.HasValue) return localOverride.Value;

        var suscripcion = await _db.SuscripcionesComerciales.AsNoTracking()
            .Where(s => s.RestauranteId == restauranteId && s.Activa &&
                s.PlanComercial.Activo && (s.VenceUtc == null || s.VenceUtc > fecha))
            .Select(s => (int?)s.PlanComercialId).FirstOrDefaultAsync();

        if (!suscripcion.HasValue && local.CadenaId.HasValue)
            suscripcion = await _db.SuscripcionesComerciales.AsNoTracking()
                .Where(s => s.CadenaId == local.CadenaId && s.Activa &&
                    s.PlanComercial.Activo && (s.VenceUtc == null || s.VenceUtc > fecha))
                .Select(s => (int?)s.PlanComercialId).FirstOrDefaultAsync();

        return suscripcion.HasValue && await _db.PlanesFuncionalidades.AsNoTracking()
            .AnyAsync(p => p.PlanComercialId == suscripcion.Value && p.FuncionalidadId == feature.Value);
    }
}
