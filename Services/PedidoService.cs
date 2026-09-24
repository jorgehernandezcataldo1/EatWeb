using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Services;

public class LineaCarritoCalculada
{
    public Guid LineaId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
    public List<PersonalizacionLegible> Personalizaciones { get; set; } = new();
    public decimal Subtotal { get; set; }
    public string Observacion { get; set; } = string.Empty;
}

public class PersonalizacionLegible
{
    public string TipoAccion { get; set; } = string.Empty; // "Sin ...", "Agregar ..."
    public string NombreIngrediente { get; set; } = string.Empty;
    public decimal PrecioExtra { get; set; }
}

public class CarritoCalculado
{
    public List<LineaCarritoCalculada> Lineas { get; set; } = new();
    public decimal Total { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class ResultadoPedido
{
    public bool Ok { get; set; }
    public int? PedidoId { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class PedidoService
{
    private readonly ApplicationDbContext _context;

    public PedidoService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Calcula los precios y detalles del carrito
    /// </summary>
    public async Task<CarritoCalculado> CalcularCarritoAsync(CarritoSesion carrito)
    {
        var resultado = new CarritoCalculado();

        if (carrito.Lineas.Count == 0)
        {
            resultado.Errores.Add("Carrito vacío");
            return resultado;
        }

        foreach (var linea in carrito.Lineas)
        {
            // Obtener producto
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == linea.ProductoId);

            if (producto == null)
            {
                resultado.Errores.Add($"Producto {linea.ProductoId} no encontrado");
                continue;
            }

            if (!producto.Activo || !producto.Disponible)
            {
                resultado.Errores.Add($"{producto.Nombre} no está disponible");
                continue;
            }

            // Obtener ingredientes del producto
            var ingredientesProducto = await _context.ProductoIngredientes
                .AsNoTracking()
                .Where(pi => pi.ProductoId == linea.ProductoId)
                .Include(pi => pi.Ingrediente)
                .ToListAsync();

            // Validar "quitar" (deben ser Incluidos)
            foreach (var idQuitar in linea.IngredientesQuitar)
            {
                var ingrediente = ingredientesProducto.FirstOrDefault(pi => pi.IngredienteId == idQuitar);
                if (ingrediente == null || ingrediente.Tipo != TipoIngrediente.Incluido)
                {
                    resultado.Errores.Add($"No se puede quitar el ingrediente {idQuitar} de {producto.Nombre}");
                    continue;
                }
                if (!ingrediente.Ingrediente.Activo)
                {
                    resultado.Errores.Add($"Ingrediente no disponible");
                    continue;
                }
            }

            // Validar "agregar" (deben ser Extra)
            foreach (var idAgregar in linea.IngredientesAgregar)
            {
                var ingrediente = ingredientesProducto.FirstOrDefault(pi => pi.IngredienteId == idAgregar);
                if (ingrediente == null || ingrediente.Tipo != TipoIngrediente.Extra)
                {
                    resultado.Errores.Add($"No se puede agregar el ingrediente {idAgregar} a {producto.Nombre}");
                    continue;
                }
                if (!ingrediente.Ingrediente.Activo)
                {
                    resultado.Errores.Add($"Ingrediente no disponible");
                    continue;
                }
            }

            // Calcular extras
            var personalizaciones = new List<PersonalizacionLegible>();
            var montoPrecioExtra = 0m;

            foreach (var idQuitar in linea.IngredientesQuitar)
            {
                var ingrediente = ingredientesProducto.First(pi => pi.IngredienteId == idQuitar);
                personalizaciones.Add(new PersonalizacionLegible
                {
                    TipoAccion = "Sin",
                    NombreIngrediente = ingrediente.Ingrediente.Nombre,
                    PrecioExtra = 0
                });
            }

            foreach (var idAgregar in linea.IngredientesAgregar)
            {
                var ingrediente = ingredientesProducto.First(pi => pi.IngredienteId == idAgregar);
                montoPrecioExtra += ingrediente.PrecioExtra;
                personalizaciones.Add(new PersonalizacionLegible
                {
                    TipoAccion = "Agregar",
                    NombreIngrediente = ingrediente.Ingrediente.Nombre,
                    PrecioExtra = ingrediente.PrecioExtra
                });
            }

            // Calcular subtotal
            var precioUnitarioConExtras = producto.Precio + montoPrecioExtra;
            var subtotal = precioUnitarioConExtras * linea.Cantidad;

            resultado.Lineas.Add(new LineaCarritoCalculada
            {
                LineaId = linea.LineaId,
                NombreProducto = producto.Nombre,
                PrecioUnitario = producto.Precio,
                Cantidad = linea.Cantidad,
                Personalizaciones = personalizaciones,
                Subtotal = subtotal,
                Observacion = linea.Observacion
            });

            resultado.Total += subtotal;
        }

        return resultado;
    }

    /// <summary>
    /// Crea un pedido con validación completa
    /// </summary>
    public async Task<ResultadoPedido> CrearPedidoAsync(
        int comensalId,
        CarritoSesion carrito,
        string? observacionGeneral)
    {
        var resultado = new ResultadoPedido();

        // Validar comensal y sesión
        var comensal = await _context.Comensales
            .Include(c => c.MesaSesion)
            .ThenInclude(s => s!.Mesa)
            .FirstOrDefaultAsync(c => c.Id == comensalId);

        if (comensal == null)
        {
            resultado.Errores.Add("Comensal no encontrado");
            return resultado;
        }

        var sesion = comensal.MesaSesion;
        if (sesion == null || sesion.FechaCierre.HasValue)
        {
            resultado.Errores.Add("Sesión cerrada o no disponible");
            return resultado;
        }

        var mesa = sesion.Mesa;
        if (!mesa.Activa)
        {
            resultado.Errores.Add("Mesa no disponible");
            return resultado;
        }

        if (sesion.CuentaSolicitadaEn.HasValue)
        {
            resultado.Errores.Add("La cuenta ha sido solicitada");
            return resultado;
        }

        if (carrito.Lineas.Count == 0)
        {
            resultado.Errores.Add("Carrito vacío");
            return resultado;
        }

        // Validar cantidades
        foreach (var linea in carrito.Lineas)
        {
            if (linea.Cantidad < 1 || linea.Cantidad > 20)
            {
                resultado.Errores.Add($"Cantidad inválida: {linea.Cantidad}");
                return resultado;
            }
        }

        // Calcular carrito
        var carritoCalculado = await CalcularCarritoAsync(carrito);
        if (carritoCalculado.Errores.Count > 0)
        {
            resultado.Errores = carritoCalculado.Errores;
            return resultado;
        }

        // Crear pedido
        var pedido = new Pedido
        {
            MesaSesionId = sesion.Id,
            ComensalId = comensalId,
            Estado = EstadoPedido.Pendiente,
            Total = carritoCalculado.Total,
            ObservacionGeneral = observacionGeneral ?? string.Empty,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(); // Guardar para obtener el Id

        // Crear detalles
        foreach (var lineaCalculada in carritoCalculado.Lineas)
        {
            var linea = carrito.Lineas.First(l => l.LineaId == lineaCalculada.LineaId);
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstAsync(p => p.Id == linea.ProductoId);

            var detalle = new DetallePedido
            {
                PedidoId = pedido.Id,
                ProductoId = linea.ProductoId,
                NombreProducto = producto.Nombre,
                PrecioUnitario = producto.Precio,
                Cantidad = linea.Cantidad,
                Observacion = linea.Observacion,
                Subtotal = lineaCalculada.Subtotal
            };

            _context.DetallesPedidos.Add(detalle);
            await _context.SaveChangesAsync(); // Guardar para obtener el Id

            // Crear personalizaciones
            foreach (var pers in lineaCalculada.Personalizaciones)
            {
                var ingredienteId = await _context.Ingredientes
                    .AsNoTracking()
                    .Where(i => i.Nombre == pers.NombreIngrediente)
                    .Select(i => i.Id)
                    .FirstAsync();

                var detalleIngrediente = new DetallePedidoIngrediente
                {
                    DetallePedidoId = detalle.Id,
                    IngredienteId = ingredienteId,
                    NombreIngrediente = pers.NombreIngrediente,
                    Accion = pers.TipoAccion == "Sin" ? AccionIngrediente.Quitar : AccionIngrediente.Agregar,
                    PrecioExtra = pers.PrecioExtra
                };

                _context.DetallesIngredientes.Add(detalleIngrediente);
            }
        }

        // Crear historial
        var historial = new HistorialEstadoPedido
        {
            PedidoId = pedido.Id,
            EstadoAnterior = null,
            EstadoNuevo = EstadoPedido.Pendiente,
            Fecha = DateTime.UtcNow,
            UsuarioId = null // Sistema/Cliente
        };

        _context.HistorialesEstadoPedido.Add(historial);

        // Guardar TODO
        await _context.SaveChangesAsync();

        resultado.Ok = true;
        resultado.PedidoId = pedido.Id;
        return resultado;
    }

    /// <summary>
    /// Cambia el estado de un pedido con validación de transiciones
    /// </summary>
    public async Task<ResultadoPedido> CambiarEstadoAsync(
        int pedidoId,
        string nuevoEstado,
        string usuarioId)
    {
        var resultado = new ResultadoPedido();

        var pedido = await _context.Pedidos.FindAsync(pedidoId);
        if (pedido == null)
        {
            resultado.Errores.Add("Pedido no encontrado");
            return resultado;
        }

        var estadoActual = pedido.Estado;

        // Validar transición
        if (!EsTransicionValida(estadoActual, nuevoEstado))
        {
            resultado.Errores.Add($"Transición de {estadoActual} a {nuevoEstado} no permitida");
            return resultado;
        }

        // Cambiar estado
        pedido.Estado = nuevoEstado;

        // Registrar en historial
        var historial = new HistorialEstadoPedido
        {
            PedidoId = pedidoId,
            EstadoAnterior = estadoActual,
            EstadoNuevo = nuevoEstado,
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId
        };

        _context.HistorialesEstadoPedido.Add(historial);
        await _context.SaveChangesAsync();

        resultado.Ok = true;
        resultado.PedidoId = pedidoId;
        return resultado;
    }

    /// <summary>
    /// Valida si una transición de estado es permitida
    /// </summary>
    private static bool EsTransicionValida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            var x when x == EstadoPedido.Pendiente => nuevoEstado == EstadoPedido.EnPreparacion || nuevoEstado == EstadoPedido.Cancelado,
            var x when x == EstadoPedido.EnPreparacion => nuevoEstado == EstadoPedido.Listo || nuevoEstado == EstadoPedido.Cancelado,
            var x when x == EstadoPedido.Listo => nuevoEstado == EstadoPedido.Entregado,
            _ => false // Entregado y Cancelado son terminales
        };
    }
}
