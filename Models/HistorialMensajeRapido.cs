namespace EatWeb.Models;

public class HistorialMensajeRapido
{
    public int Id { get; set; }

    public int MensajeRapidoId { get; set; }
    public MensajeRapido MensajeRapido { get; set; } = null!;

    public int RestauranteId { get; set; }
    public Restaurante Restaurante { get; set; } = null!;

    public int EstacionId { get; set; }
    public Estacion Estacion { get; set; } = null!;

    public string GarzonId { get; set; } = string.Empty;
    public ApplicationUser Garzon { get; set; } = null!;

    public DateTime EnviadoEn { get; set; } = DateTime.UtcNow;

    public bool Leido { get; set; } = false;
    public DateTime? LeidoEn { get; set; }
}
