using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class Pago
{
    public int Id { get; set; }
    public int CuentaId { get; set; }
    public int? ComensalId { get; set; }
    public string Estado { get; set; } = EstadoPago.Pendiente;
    public string Metodo { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public string? RequestFingerprint { get; set; }

    public decimal Monto { get; set; }

    // Propina mantiene el monto final CLP por compatibilidad.
    // Cuando se usa porcentaje, guardamos también base y porcentaje como snapshot.
    public decimal Propina { get; set; }
    public decimal? PropinaPorcentaje { get; set; }
    public decimal? PropinaBase { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaConfirmacion { get; set; }

    public Cuenta? Cuenta { get; set; }
    public Comensal? Comensal { get; set; }
    public ICollection<PagoDetalle> Detalles { get; set; } = new List<PagoDetalle>();
}
