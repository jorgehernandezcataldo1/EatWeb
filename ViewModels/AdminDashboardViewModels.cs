namespace EatWeb.ViewModels;

public class AdminCadenaDashboardViewModel
{
    public string CadenaNombre { get; set; } = "Mi cadena";
    public decimal VentasHoy { get; set; }
    public int PedidosHoy { get; set; }
    public int TotalMesas { get; set; }
    public int MesasOcupadas { get; set; }
    public int SolicitudesPendientes { get; set; }
    public List<RestauranteDashboardItemViewModel> Restaurantes { get; set; } = new();

    public int MesasDisponibles => Math.Max(0, TotalMesas - MesasOcupadas);
    public decimal OcupacionPorcentaje =>
        TotalMesas == 0 ? 0 : Math.Round(MesasOcupadas * 100m / TotalMesas, 0);
}

public class RestauranteDashboardItemViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public decimal VentasHoy { get; set; }
    public int PedidosHoy { get; set; }
    public int TotalMesas { get; set; }
    public int MesasOcupadas { get; set; }
    public int SolicitudesPendientes { get; set; }
    public int PedidosAtrasados { get; set; }

    public decimal OcupacionPorcentaje =>
        TotalMesas == 0 ? 0 : Math.Round(MesasOcupadas * 100m / TotalMesas, 0);
}

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
    public int PedidosPendientes { get; set; }
    public int PedidosEnPreparacion { get; set; }
    public int PedidosListos { get; set; }
    public int SolicitudesPendientes { get; set; }
    public decimal VentasHoy { get; set; }
    public decimal PropinasHoy { get; set; }

    public double TiempoPreparacionPromedioMin { get; set; }
    public double TiempoAtencionPromedioMin { get; set; }
    public int PedidosAtrasados { get; set; }
    public int MesasMasDe90Min { get; set; }

    public List<PilotChecklistItemViewModel> Checklist { get; set; } = new();

    public int MesasDisponibles => Math.Max(0, TotalMesas - MesasOcupadas);
    public decimal OcupacionPorcentaje =>
        TotalMesas == 0 ? 0 : Math.Round(MesasOcupadas * 100m / TotalMesas, 0);

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
