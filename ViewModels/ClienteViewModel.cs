namespace EatWeb.ViewModels;

/// <summary>Pantalla "¿Cómo te llamas?" al escanear el QR.</summary>
public class IngresoViewModel
{
    public int MesaNumero { get; set; }
    public string RestauranteNombre { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Mensaje simple para el cliente (mesa inválida, sesión terminada...).</summary>
public class AvisoClienteViewModel
{
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class CartaViewModel
{
    public List<CartaCategoriaViewModel> Categorias { get; set; } = new();
}

public class CartaCategoriaViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<CartaProductoViewModel> Productos { get; set; } = new();
}

public class CartaProductoViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Disponible { get; set; }
}