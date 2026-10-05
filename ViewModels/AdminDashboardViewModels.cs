namespace EatWeb.ViewModels;

public class AdminRestauranteDashboardViewModel
{
    public int RestauranteId { get; set; }
    public string RestauranteNombre { get; set; } = string.Empty;
    public bool RestauranteActivo { get; set; }

    public int TotalProductos { get; set; }
    public int ProductosDisponibles { get; set; }
    public int TotalMesas { get; set; }
    public int MesasOcupadas { get; set; }
    public int TotalPedidosHoy { get; set; }
    public int PedidosActivos { get; set; }
    public int SolicitudesPendientes { get; set; }
    public decimal VentasHoy { get; set; }
    public decimal PropinasHoy { get; set; }

    public List<PilotChecklistItemViewModel> Checklist { get; set; } = new();

    public bool ListoParaPiloto =>
        RestauranteActivo &&
        Checklist.Count > 0 &&
        Checklist.All(x => x.Ok);
}

public class PilotChecklistItemViewModel
{
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = "Index";
}
