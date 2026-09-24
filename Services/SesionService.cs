using EatWeb.Data;
using EatWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class ContextoComensal
{
    public int ComensalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int SesionId { get; set; }
    public int MesaId { get; set; }
    public int MesaNumero { get; set; }
    public string RestauranteNombre { get; set; } = string.Empty;
    public bool CuentaSolicitada { get; set; }
}

public class SesionService
{
    private readonly ApplicationDbContext _context;

    public SesionService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene la sesión abierta de una mesa o la crea
    /// </summary>
    public async Task<MesaSesion> ObtenerOCrearSesionAbiertaAsync(int mesaId)
    {
        // Intentar obtener sesión abierta
        var sesion = await _context.MesaSesiones
            .FirstOrDefaultAsync(s => s.MesaId == mesaId && s.FechaCierre == null);

        if (sesion != null)
            return sesion;

        // Crear nueva sesión
        sesion = new MesaSesion
        {
            MesaId = mesaId,
            FechaApertura = DateTime.UtcNow,
            FechaCierre = null,
            CuentaSolicitadaEn = null
        };

        try
        {
            _context.MesaSesiones.Add(sesion);
            await _context.SaveChangesAsync();
            return sesion;
        }
        catch (DbUpdateException)
        {
            // Otra solicitud creó la sesión simultáneamente, releer
            var sesionExistente = await _context.MesaSesiones
                .FirstOrDefaultAsync(s => s.MesaId == mesaId && s.FechaCierre == null);

            if (sesionExistente == null)
                throw;

            return sesionExistente;
        }
    }

    /// <summary>
    /// Crea un nuevo comensal en una sesión
    /// </summary>
    public async Task<Guid> CrearComensalAsync(int sesionId, string nombre)
    {
        // Validar cantidad máxima de comensales
        var cantidad = await _context.Comensales
            .CountAsync(c => c.MesaSesionId == sesionId);

        if (cantidad >= 20)
            throw new InvalidOperationException("Máximo 20 comensales por sesión");

        var token = Guid.NewGuid();
        var comensal = new Comensal
        {
            MesaSesionId = sesionId,
            Nombre = nombre,
            Token = token,
            FechaIngreso = DateTime.UtcNow
        };

        _context.Comensales.Add(comensal);
        await _context.SaveChangesAsync();

        return token;
    }

    /// <summary>
    /// Obtiene el contexto de un comensal por su token
    /// </summary>
    public async Task<ContextoComensal?> ObtenerContextoAsync(Guid token)
    {
        var comensal = await _context.Comensales
            .AsNoTracking()
            .Include(c => c.MesaSesion)
            .ThenInclude(s => s!.Mesa)
            .ThenInclude(m => m.Restaurante)
            .FirstOrDefaultAsync(c => c.Token == token);

        if (comensal == null)
            return null;

        var sesion = comensal.MesaSesion;
        if (sesion == null || sesion.FechaCierre.HasValue)
            return null; // Sesión cerrada

        var mesa = sesion.Mesa;
        if (!mesa.Activa)
            return null; // Mesa inactiva

        return new ContextoComensal
        {
            ComensalId = comensal.Id,
            Nombre = comensal.Nombre,
            SesionId = sesion.Id,
            MesaId = mesa.Id,
            MesaNumero = mesa.Numero,
            RestauranteNombre = mesa.Restaurante?.Nombre ?? "N/A",
            CuentaSolicitada = sesion.CuentaSolicitadaEn.HasValue
        };
    }

    /// <summary>
    /// Solicita la cuenta
    /// </summary>
    public async Task SolicitarCuentaAsync(int sesionId)
    {
        var sesion = await _context.MesaSesiones.FindAsync(sesionId);
        if (sesion != null && !sesion.CuentaSolicitadaEn.HasValue)
        {
            sesion.CuentaSolicitadaEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Anula la solicitud de cuenta
    /// </summary>
    public async Task AnularCuentaAsync(int sesionId)
    {
        var sesion = await _context.MesaSesiones.FindAsync(sesionId);
        if (sesion != null)
        {
            sesion.CuentaSolicitadaEn = null;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Cierra una sesión
    /// </summary>
    public async Task<(bool Success, string? Error)> CerrarSesionAsync(
        int sesionId,
        bool forzar,
        string? usuarioId)
    {
        var sesion = await _context.MesaSesiones
            .Include(s => s.Comensales)
            .ThenInclude(c => c.Pedidos)
            .FirstOrDefaultAsync(s => s.Id == sesionId);

        if (sesion == null)
            return (false, "Sesión no encontrada");

        // Verificar pedidos activos
        var pedidosActivos = sesion.Comensales
            .SelectMany(c => c.Pedidos)
            .Where(p => p.Estado == Models.Enums.EstadoPedido.Pendiente ||
                       p.Estado == Models.Enums.EstadoPedido.EnPreparacion ||
                       p.Estado == Models.Enums.EstadoPedido.Listo)
            .ToList();

        if (pedidosActivos.Count > 0 && !forzar)
            return (false, "Hay pedidos activos");

        // Si se fuerza, cancelar pedidos activos
        if (forzar && pedidosActivos.Count > 0)
        {
            foreach (var pedido in pedidosActivos)
            {
                pedido.Estado = Models.Enums.EstadoPedido.Cancelado;
                _context.HistorialesEstadoPedido.Add(new HistorialEstadoPedido
                {
                    PedidoId = pedido.Id,
                    EstadoAnterior = pedido.Estado,
                    EstadoNuevo = Models.Enums.EstadoPedido.Cancelado,
                    Fecha = DateTime.UtcNow,
                    UsuarioId = usuarioId
                });
            }
        }

        // Cerrar sesión
        sesion.FechaCierre = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }
}
