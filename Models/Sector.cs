namespace EatWeb.Models;

public class Sector
{
    public int Id { get; set; }
    public int RestauranteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public Restaurante? Restaurante { get; set; }
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
}
