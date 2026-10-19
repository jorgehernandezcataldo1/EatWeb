using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class MesasIndexViewModel
{
    public List<MesaViewModel> Mesas { get; set; } = new();
    public List<SolicitudMesaResumenViewModel> SolicitudesPendientes { get; set; } = new();
    public List<AccionGarzonViewModel> AccionesPrioritarias { get; set; } = new();
}

public class AccionGarzonViewModel
{
    public int SesionId { get; set; }
    public int MesaNumero { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int Prioridad { get; set; }
}

/// <summary>Una fila/tarjeta del tablero de mesas.</summary>
public class MesaViewModel
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public bool Activa { get; set; }
    public string? GarzonNombre { get; set; }
    public bool TieneSesionAbierta { get; set; }
    public int? SesionId { get; set; }
    public int? SectorId { get; set; }
    public string SectorNombre { get; set; } = "Sin sector";
    public int OrdenEnSector { get; set; }
    public DateTime? FechaApertura { get; set; }
    public int Comensales { get; set; }
    public int SolicitudesPendientes { get; set; }
    public decimal TotalAcumulado { get; set; }
    public string Estado { get; set; } = string.Empty; // EstadoMesaVisual
}

public class MesaFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El número de mesa es requerido")]
    [Range(1, 999, ErrorMessage = "El número debe estar entre 1 y 999")]
    public int Numero { get; set; }

    public string? GarzonId { get; set; }
    public int? SectorId { get; set; }
    public int OrdenEnSector { get; set; }
    public bool Activa { get; set; } = true;
}

public class MesaQrViewModel
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public string Url { get; set; } = string.Empty;
}