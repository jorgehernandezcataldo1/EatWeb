namespace EatWeb.Models.Enums;

public static class TipoSolicitudMesa
{
    public const string LlamarGarzon = "LlamarGarzon";
    public const string PedirCuenta = "PedirCuenta";
    public const string SolicitarAlgo = "SolicitarAlgo";

    public static IReadOnlyCollection<string> Todos() =>
        new[] { LlamarGarzon, PedirCuenta, SolicitarAlgo };
}
