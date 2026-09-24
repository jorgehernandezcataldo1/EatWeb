using System.Text.Json;
using EatWeb.Models.Enums;

namespace EatWeb.Services;

public class LineaCarrito
{
    public Guid LineaId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public List<int> IngredientesQuitar { get; set; } = new();
    public List<int> IngredientesAgregar { get; set; } = new();
    public string Observacion { get; set; } = string.Empty;
}

public class CarritoSesion
{
    public int ComensalId { get; set; }
    public List<LineaCarrito> Lineas { get; set; } = new();
}

public class CarritoService
{
    private const string KeyCarrito = "carrito";
    private readonly IHttpContextAccessor _contextAccessor;

    public CarritoService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    private ISession? GetSession() => _contextAccessor.HttpContext?.Session;

    /// <summary>
    /// Obtiene el carrito de una sesión
    /// </summary>
    public CarritoSesion Obtener(int comensalId)
    {
        var session = GetSession();
        if (session == null)
            return new CarritoSesion { ComensalId = comensalId };

        if (session.TryGetValue(KeyCarrito, out var data))
        {
            var carrito = JsonSerializer.Deserialize<CarritoSesion>(data!);
            if (carrito != null && carrito.ComensalId == comensalId)
                return carrito;
        }

        return new CarritoSesion { ComensalId = comensalId };
    }

    /// <summary>
    /// Guarda el carrito en sesión
    /// </summary>
    private void Guardar(CarritoSesion carrito)
    {
        var session = GetSession();
        if (session != null)
        {
            var json = JsonSerializer.Serialize(carrito);
            session.SetString(KeyCarrito, json);
        }
    }

    /// <summary>
    /// Agrega una línea al carrito (o suma cantidad si es idéntica)
    /// </summary>
    public void Agregar(int comensalId, int productoId, int cantidad,
        List<int> ingredientesQuitar, List<int> ingredientesAgregar, string observacion)
    {
        var carrito = Obtener(comensalId);

        // Buscar línea idéntica
        var lineaExistente = carrito.Lineas.FirstOrDefault(l =>
            l.ProductoId == productoId &&
            l.IngredientesQuitar.SequenceEqual(ingredientesQuitar) &&
            l.IngredientesAgregar.SequenceEqual(ingredientesAgregar) &&
            l.Observacion == observacion);

        if (lineaExistente != null)
        {
            lineaExistente.Cantidad = Math.Min(lineaExistente.Cantidad + cantidad, 20);
        }
        else
        {
            carrito.Lineas.Add(new LineaCarrito
            {
                LineaId = Guid.NewGuid(),
                ProductoId = productoId,
                Cantidad = cantidad,
                IngredientesQuitar = ingredientesQuitar,
                IngredientesAgregar = ingredientesAgregar,
                Observacion = observacion
            });
        }

        Guardar(carrito);
    }

    /// <summary>
    /// Cambia la cantidad de una línea
    /// </summary>
    public void CambiarCantidad(int comensalId, Guid lineaId, int delta)
    {
        var carrito = Obtener(comensalId);
        var linea = carrito.Lineas.FirstOrDefault(l => l.LineaId == lineaId);

        if (linea != null)
        {
            linea.Cantidad = Math.Max(1, Math.Min(linea.Cantidad + delta, 20));
            if (linea.Cantidad == 0)
            {
                carrito.Lineas.Remove(linea);
            }
        }

        Guardar(carrito);
    }

    /// <summary>
    /// Quita una línea del carrito
    /// </summary>
    public void Quitar(int comensalId, Guid lineaId)
    {
        var carrito = Obtener(comensalId);
        var linea = carrito.Lineas.FirstOrDefault(l => l.LineaId == lineaId);

        if (linea != null)
        {
            carrito.Lineas.Remove(linea);
            Guardar(carrito);
        }
    }

    /// <summary>
    /// Actualiza la observación de una línea
    /// </summary>
    public void ActualizarObservacion(int comensalId, Guid lineaId, string observacion)
    {
        var carrito = Obtener(comensalId);
        var linea = carrito.Lineas.FirstOrDefault(l => l.LineaId == lineaId);

        if (linea != null)
        {
            linea.Observacion = observacion ?? string.Empty;
            Guardar(carrito);
        }
    }

    /// <summary>
    /// Vacía el carrito
    /// </summary>
    public void Vaciar(int comensalId)
    {
        var session = GetSession();
        if (session != null)
        {
            session.Remove(KeyCarrito);
        }
    }

    /// <summary>
    /// Obtiene la cantidad total de items en el carrito
    /// </summary>
    public int CantidadTotal(int comensalId) => Obtener(comensalId).Lineas.Sum(l => l.Cantidad);
}
