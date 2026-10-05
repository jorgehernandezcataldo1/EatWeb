namespace EatWeb.ViewModels;

public class SolicitudMesaResumenViewModel
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    public int MesaNumero { get; set; }
    public string ComensalNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaResolucion { get; set; }
}
