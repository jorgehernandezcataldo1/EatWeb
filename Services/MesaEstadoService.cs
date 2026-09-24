using EatWeb.Models.Enums;

namespace EatWeb.Services;

/// <summary>
/// Servicio estático para calcular el estado visual de una mesa
/// </summary>
public static class MesaEstadoService
{
    public static string Calcular(
        bool activa,
        bool haySesionAbierta,
        bool cuentaSolicitada,
        IEnumerable<string> estadosPedidos)
    {
        // Prioridad exacta según especificación:
        if (!activa)
            return EstadoMesaVisual.FueraDeServicio;

        if (!haySesionAbierta)
            return EstadoMesaVisual.Disponible;

        if (cuentaSolicitada)
            return EstadoMesaVisual.CuentaSolicitada;

        var estadosList = estadosPedidos.ToList();

        if (estadosList.Any(e => e == EstadoPedido.Pendiente))
            return EstadoMesaVisual.PedidoNuevo;

        if (estadosList.Any(e => e == EstadoPedido.Listo))
            return EstadoMesaVisual.ListoParaEntregar;

        if (estadosList.Any(e => e == EstadoPedido.EnPreparacion))
            return EstadoMesaVisual.EnPreparacion;

        return EstadoMesaVisual.Ocupada;
    }
}
