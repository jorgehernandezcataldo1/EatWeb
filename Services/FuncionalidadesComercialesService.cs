using EatWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public sealed class FuncionalidadesComercialesService
{
    private readonly ApplicationDbContext _db;
    public FuncionalidadesComercialesService(ApplicationDbContext db) => _db = db;

    public async Task<bool> PuedeUsarModuloAsync(int restauranteId, string clave)
    {
        // Las funciones todavía no registradas mantienen la operación del MVP.
        // Una vez creado el código en SuperAdmin, se aplican los planes/overrides.
        var configurada = await _db.Funcionalidades.AsNoTracking()
            .AnyAsync(f => f.Clave == clave);
        return !configurada || await HabilitadaAsync(restauranteId, clave);
    }

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

        // El plan propio tiene precedencia INCLUSO si está suspendido o vencido.
        // Un plan local inactivo no debe reaparecer como acceso por herencia.
        var suscripcionLocal = await _db.SuscripcionesComerciales.AsNoTracking()
            .Where(s => s.RestauranteId == restauranteId)
            .Select(s => new
            {
                s.PlanComercialId,
                s.Activa,
                s.VenceUtc
            }).FirstOrDefaultAsync();

        if (suscripcionLocal != null)
        {
            if (!suscripcionLocal.Activa ||
                (suscripcionLocal.VenceUtc.HasValue && suscripcionLocal.VenceUtc <= fecha))
                return false;

            return await _db.PlanesFuncionalidades.AsNoTracking()
                .AnyAsync(p => p.PlanComercialId == suscripcionLocal.PlanComercialId &&
                    p.FuncionalidadId == feature.Value);
        }

        if (!local.CadenaId.HasValue) return false;

        var planCadena = await _db.SuscripcionesComerciales.AsNoTracking()
            .Where(s => s.CadenaId == local.CadenaId && s.Activa &&
                (s.VenceUtc == null || s.VenceUtc > fecha))
            .Select(s => (int?)s.PlanComercialId).FirstOrDefaultAsync();

        return planCadena.HasValue && await _db.PlanesFuncionalidades.AsNoTracking()
            .AnyAsync(p => p.PlanComercialId == planCadena.Value &&
                p.FuncionalidadId == feature.Value);
    }
}
