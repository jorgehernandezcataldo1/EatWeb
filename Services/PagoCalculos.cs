namespace EatWeb.Services;

/// <summary>
/// Reglas puras de cálculo de pago que pueden probarse sin base de datos.
/// </summary>
public static class PagoCalculos
{
    public static decimal CalcularPropina(decimal baseConsumo, decimal porcentaje)
    {
        if (baseConsumo < 0 || decimal.Truncate(baseConsumo) != baseConsumo)
            throw new ArgumentOutOfRangeException(nameof(baseConsumo));

        if (porcentaje < 0 || porcentaje > 100 || decimal.Round(porcentaje, 2) != porcentaje)
            throw new ArgumentOutOfRangeException(nameof(porcentaje));

        return Math.Round(
            baseConsumo * porcentaje / 100m,
            0,
            MidpointRounding.AwayFromZero);
    }
}
