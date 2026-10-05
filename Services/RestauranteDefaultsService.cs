using EatWeb.Data;
using EatWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class RestauranteDefaultsService
{
    private readonly ApplicationDbContext _context;

    public RestauranteDefaultsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AsegurarEstacionesAsync(int restauranteId)
    {
        var existentes = await _context.Estaciones
            .Where(e => e.RestauranteId == restauranteId)
            .Select(e => e.Nombre)
            .ToListAsync();

        if (!existentes.Contains("Cocina"))
            _context.Estaciones.Add(new Estacion { RestauranteId = restauranteId, Nombre = "Cocina", Orden = 1 });

        if (!existentes.Contains("Bar"))
            _context.Estaciones.Add(new Estacion { RestauranteId = restauranteId, Nombre = "Bar", Orden = 2 });

        await _context.SaveChangesAsync();
    }
}
