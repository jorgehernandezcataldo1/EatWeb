using System.ComponentModel.DataAnnotations;

namespace EatWeb.ViewModels;

public class SectorViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(80)]
    public string Nombre { get; set; } = string.Empty;

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public int CantidadMesas { get; set; }
    public List<int> MesaIds { get; set; } = new();
    public List<SectorMesaOpcionViewModel> MesasDisponibles { get; set; } = new();
}

public class SectorMesaOpcionViewModel
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public bool Seleccionada { get; set; }
    public string? SectorActual { get; set; }
}
