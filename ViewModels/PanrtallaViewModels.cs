namespace EatWeb.ViewModels;

public class PantallaViewModel
{
    public int? EstacionId { get; set; }
    public string Titulo { get; set; } = "Todas las estaciones";
    public int TotalPorAtender { get; set; }
    public List<PantallaTabViewModel> Tabs { get; set; } = new();
    public List<PantallaItemViewModel> Pendientes { get; set; } = new();
    public List<PantallaItemViewModel> EnPreparacion { get; set; } = new();
    public List<PantallaItemViewModel> Listos { get; set; } = new();
}

public class PantallaTabViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int PorAtender { get; set; }
}

public class PantallaItemViewModel
{
    public int DetalleId { get; set; }
    public int MesaNumero { get; set; }
    public string Comensal { get; set; } = string.Empty;
    public string Estacion { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string? Observacion { get; set; }
    public string? NotaPedido { get; set; }
    public int Minutos { get; set; }
    public List<string> Personalizaciones { get; set; } = new();
}