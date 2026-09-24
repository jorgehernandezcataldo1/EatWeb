using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

/// <summary>Una fila/tarjeta del tablero de mesas.</summary>
public class MesaViewModel
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public bool Activa { get; set; }
    public string? GarzonNombre { get; set; }
    public bool TieneSesionAbierta { get; set; }
    public int Comensales { get; set; }
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
    public bool Activa { get; set; } = true;
}

public class MesaQrViewModel
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public string Url { get; set; } = string.Empty;
}