namespace EatWeb.Models;

public class Mesa
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public int Numero { get; set; }
    public string CodigoQr { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public string? GarzonId { get; set; }

    // Relaciones
    public Restaurante? Restaurante { get; set; }
    public ApplicationUser? Garzon { get; set; }
    public ICollection<MesaSesion> Sesiones { get; set; } = new List<MesaSesion>();
}
