namespace EatWeb.Models;

public class Comensal
{
    public int Id { get; set; }
    public int MesaSesionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Guid Token { get; set; }
    public DateTime FechaIngreso { get; set; }

    // Relaciones
    public MesaSesion? MesaSesion { get; set; }
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
