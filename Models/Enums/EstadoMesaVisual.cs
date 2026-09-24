namespace EatWeb.Models.Enums;

public static class EstadoMesaVisual
{
    public const string FueraDeServicio = nameof(FueraDeServicio);
    public const string Disponible = nameof(Disponible);
    public const string CuentaSolicitada = nameof(CuentaSolicitada);
    public const string PedidoNuevo = nameof(PedidoNuevo);
    public const string ListoParaEntregar = nameof(ListoParaEntregar);
    public const string EnPreparacion = nameof(EnPreparacion);
    public const string Ocupada = nameof(Ocupada);

    public static IEnumerable<string> Todos() =>
        new[] { FueraDeServicio, Disponible, CuentaSolicitada, PedidoNuevo, ListoParaEntregar, EnPreparacion, Ocupada };
}
