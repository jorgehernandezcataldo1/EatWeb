namespace EatWeb.Models.Enums;

public static class EstadoSolicitudMesa
{
    public const string Pendiente = "Pendiente";
    public const string Atendida = "Atendida";
    public const string Cancelada = "Cancelada";

    public static IReadOnlyCollection<string> Todos() =>
        new[] { Pendiente, Atendida, Cancelada };
}
