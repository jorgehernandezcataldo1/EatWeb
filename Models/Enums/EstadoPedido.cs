namespace EatWeb.Models.Enums;

public static class EstadoPedido
{
    public const string Pendiente = nameof(Pendiente);
    public const string EnPreparacion = nameof(EnPreparacion);
    public const string Listo = nameof(Listo);
    public const string Entregado = nameof(Entregado);
    public const string Cancelado = nameof(Cancelado);

    public static IEnumerable<string> Todos() =>
        new[] { Pendiente, EnPreparacion, Listo, Entregado, Cancelado };
}
