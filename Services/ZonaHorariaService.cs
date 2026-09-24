namespace EatWeb.Services;

/// <summary>
/// Servicio estático para manejo de zona horaria (Santiago de Chile)
/// </summary>
public static class ZonaHorariaService
{
    private static readonly TimeZoneInfo _zonaHoraria;

    static ZonaHorariaService()
    {
        // Intentar obtener la zona horaria de Santiago
        _zonaHoraria = TimeZoneInfo.TryFindSystemTimeZoneById("Pacific SA Standard Time", out var tz)
            ? tz
            : TimeZoneInfo.TryFindSystemTimeZoneById("America/Santiago", out tz)
                ? tz
                : TimeZoneInfo.FindSystemTimeZoneById("UTC"); // Fallback
    }

    /// <summary>
    /// Convierte una hora UTC a la hora local de Santiago
    /// </summary>
    public static DateTime ALocal(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
            utc = new DateTime(utc.Ticks, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _zonaHoraria);
    }

    /// <summary>
    /// Obtiene el inicio del día local (0:00:00 Santiago) convertido a UTC
    /// </summary>
    public static DateTime InicioDelDiaUtc()
    {
        var ahora = DateTime.UtcNow;
        var horaLocal = ALocal(ahora);
        var inicioDelDia = horaLocal.Date; // 0:00:00

        // Convertir de vuelta a UTC
        return TimeZoneInfo.ConvertTimeToUtc(inicioDelDia, _zonaHoraria);
    }
}
