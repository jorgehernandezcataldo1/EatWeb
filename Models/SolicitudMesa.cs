using EatWeb.Models.Enums;

namespace EatWeb.Models;

public class SolicitudMesa
{
    public int Id { get; set; }
    public int MesaSesionId { get; set; }
    public int ComensalId { get; set; }
    public string Tipo { get; set; } = TipoSolicitudMesa.LlamarGarzon;
    public string? Mensaje { get; set; }
    public string Estado { get; set; } = EstadoSolicitudMesa.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; set; }
    public string? AtendidaPorId { get; set; }

    public MesaSesion? MesaSesion { get; set; }
    public Comensal? Comensal { get; set; }
    public ApplicationUser? AtendidaPor { get; set; }
}
