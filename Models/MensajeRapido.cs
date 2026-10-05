namespace EatWeb.Models;

public class MensajeRapido
{
    public int Id { get; set; }

    public int RestauranteId { get; set; }
    public Restaurante Restaurante { get; set; } = null!;

    public int EstacionId { get; set; }
    public Estacion Estacion { get; set; } = null!;

    public string Texto { get; set; } = string.Empty;

    public int Orden { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relación inversa
    public ICollection<HistorialMensajeRapido> Historial { get; set; } = new List<HistorialMensajeRapido>();
}
