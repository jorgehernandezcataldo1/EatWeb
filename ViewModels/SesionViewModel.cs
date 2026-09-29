namespace EatWeb.ViewModels;

public class SesionesIndexViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public List<SesionHistorialViewModel> Sesiones { get; set; } = new();
    public decimal TotalPeriodo { get; set; }
    public int CantidadSesiones { get; set; }
}

public class SesionHistorialViewModel
{
    public int Id { get; set; }
    public int MesaNumero { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public bool CuentaSolicitada { get; set; }
    public int Comensales { get; set; }
    public int TotalPedidos { get; set; }
    public decimal Total { get; set; }
}