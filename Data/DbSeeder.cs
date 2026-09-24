using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public DbSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task SembrarAsync(IServiceProvider serviceProvider)
    {
        // Aplicar migraciones
        await _context.Database.MigrateAsync();

        // Crear Restaurante Demo si no existe
        var restaurante = await _context.Restaurantes.FirstOrDefaultAsync(r => r.Nombre == "Restaurante Demo");
        if (restaurante == null)
        {
            restaurante = new Restaurante
            {
                Nombre = "Restaurante Demo",
                Activo = true
            };
            _context.Restaurantes.Add(restaurante);
            await _context.SaveChangesAsync();
        }

        // Crear roles
        var roles = new[] { "Admin", "Garzon" };
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Crear usuario admin
        var adminEmail = _configuration["SeedAdmin:Email"] ?? "admin@restaurante.local";
        var existingAdmin = await _userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin == null)
        {
            var adminPassword = _configuration["SeedAdmin:Password"] ?? "Admin123!";
            var adminNombre = _configuration["SeedAdmin:Nombre"] ?? "Administrador";

            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                NombreCompleto = adminNombre,
                RestauranteId = restaurante.Id,
                Activo = true
            };

            var result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        // Sembrar datos demo solo si no existen categorías
        if (!await _context.Categorias.AnyAsync())
        {
            // Categorías
            var categorias = new Dictionary<string, int>
            {
                { "Pizzas", 1 },
                { "Hamburguesas", 2 },
                { "Acompañamientos", 3 },
                { "Bebidas", 4 },
                { "Postres", 5 }
            };

            var categoriasDb = new Dictionary<string, Categoria>();
            foreach (var kvp in categorias)
            {
                var categoria = new Categoria
                {
                    RestauranteId = restaurante.Id,
                    Nombre = kvp.Key,
                    Orden = kvp.Value,
                    Activa = true
                };
                _context.Categorias.Add(categoria);
                categoriasDb[kvp.Key] = categoria;
            }
            await _context.SaveChangesAsync();

            // Ingredientes
            var ingredientesNombres = new[]
            {
                "Salsa de tomate", "Queso", "Pepperoni", "Aceitunas", "Champiñones",
                "Tocino", "Tomate", "Albahaca", "Pan", "Carne", "Lechuga", "Palta", "Huevo"
            };

            var ingredientesDb = new Dictionary<string, Ingrediente>();
            foreach (var nombre in ingredientesNombres)
            {
                var ingrediente = new Ingrediente
                {
                    RestauranteId = restaurante.Id,
                    Nombre = nombre,
                    Activo = true
                };
                _context.Ingredientes.Add(ingrediente);
                ingredientesDb[nombre] = ingrediente;
            }
            await _context.SaveChangesAsync();

            // Productos
            var pizzaAmericana = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Pizzas"].Id,
                Nombre = "Pizza Americana",
                Descripcion = "Pizza deliciosa con ingredientes especiales",
                Precio = 12990,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(pizzaAmericana);

            var pizzaNapolitana = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Pizzas"].Id,
                Nombre = "Pizza Napolitana",
                Descripcion = "Clásica pizza italiana",
                Precio = 11990,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(pizzaNapolitana);

            var hamburguesa = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Hamburguesas"].Id,
                Nombre = "Hamburguesa Clásica",
                Descripcion = "Deliciosa hamburguesa artesanal",
                Precio = 8990,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(hamburguesa);

            var papas = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Acompañamientos"].Id,
                Nombre = "Papas Fritas",
                Descripcion = "Papas fritas crujientes",
                Precio = 3990,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(papas);

            var coca = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Bebidas"].Id,
                Nombre = "Coca-Cola",
                Descripcion = "Bebida refrescante",
                Precio = 3000,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(coca);

            var cerveza = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Bebidas"].Id,
                Nombre = "Cerveza",
                Descripcion = "Cerveza fría",
                Precio = 3500,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(cerveza);

            var brownie = new Producto
            {
                RestauranteId = restaurante.Id,
                CategoriaId = categoriasDb["Postres"].Id,
                Nombre = "Brownie",
                Descripcion = "Brownie de chocolate",
                Precio = 3900,
                Activo = true,
                Disponible = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Productos.Add(brownie);

            await _context.SaveChangesAsync();

            // Personalización Pizza Americana
            var pizzaAmericanaIngredientes = new[]
            {
                ("Salsa de tomate", TipoIngrediente.Incluido, 0m),
                ("Queso", TipoIngrediente.Incluido, 0m),
                ("Pepperoni", TipoIngrediente.Incluido, 0m),
                ("Aceitunas", TipoIngrediente.Incluido, 0m),
                ("Champiñones", TipoIngrediente.Extra, 0m),
                ("Tocino", TipoIngrediente.Extra, 1200m)
            };

            foreach (var (nombreIng, tipo, precio) in pizzaAmericanaIngredientes)
            {
                _context.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    ProductoId = pizzaAmericana.Id,
                    IngredienteId = ingredientesDb[nombreIng].Id,
                    Tipo = tipo,
                    PrecioExtra = precio
                });
            }

            // Personalización Pizza Napolitana
            var pizzaNapolitanaIngredientes = new[]
            {
                ("Salsa de tomate", TipoIngrediente.Incluido, 0m),
                ("Queso", TipoIngrediente.Incluido, 0m),
                ("Tomate", TipoIngrediente.Incluido, 0m),
                ("Albahaca", TipoIngrediente.Incluido, 0m),
                ("Aceitunas", TipoIngrediente.Extra, 800m),
                ("Champiñones", TipoIngrediente.Extra, 900m)
            };

            foreach (var (nombreIng, tipo, precio) in pizzaNapolitanaIngredientes)
            {
                _context.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    ProductoId = pizzaNapolitana.Id,
                    IngredienteId = ingredientesDb[nombreIng].Id,
                    Tipo = tipo,
                    PrecioExtra = precio
                });
            }

            // Personalización Hamburguesa
            var hamburguesaIngredientes = new[]
            {
                ("Pan", TipoIngrediente.Incluido, 0m),
                ("Carne", TipoIngrediente.Incluido, 0m),
                ("Lechuga", TipoIngrediente.Incluido, 0m),
                ("Tomate", TipoIngrediente.Incluido, 0m),
                ("Queso", TipoIngrediente.Incluido, 0m),
                ("Tocino", TipoIngrediente.Extra, 1200m),
                ("Palta", TipoIngrediente.Extra, 1000m),
                ("Huevo", TipoIngrediente.Extra, 800m)
            };

            foreach (var (nombreIng, tipo, precio) in hamburguesaIngredientes)
            {
                _context.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    ProductoId = hamburguesa.Id,
                    IngredienteId = ingredientesDb[nombreIng].Id,
                    Tipo = tipo,
                    PrecioExtra = precio
                });
            }

            await _context.SaveChangesAsync();

            // Crear mesas
            var random = new Random();
            for (int i = 1; i <= 5; i++)
            {
                var codigoQr = GenerarCodigoQr();
                var mesa = new Mesa
                {
                    RestauranteId = restaurante.Id,
                    Numero = i,
                    CodigoQr = codigoQr,
                    Activa = true
                };
                _context.Mesas.Add(mesa);
            }
            await _context.SaveChangesAsync();

            // Crear garzón
            var garzonEmail = "garzon@restaurante.local";
            var existingGarzon = await _userManager.FindByEmailAsync(garzonEmail);
            if (existingGarzon == null)
            {
                var garzon = new ApplicationUser
                {
                    UserName = garzonEmail,
                    Email = garzonEmail,
                    NombreCompleto = "Carlos Garzón",
                    RestauranteId = restaurante.Id,
                    Activo = true
                };
                var result = await _userManager.CreateAsync(garzon, "Garzon123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(garzon, "Garzon");

                    // Asignar garzón a las mesas 1, 2 y 3
                    var mesas = await _context.Mesas
                        .Where(m => m.RestauranteId == restaurante.Id && new[] { 1, 2, 3 }.Contains(m.Numero))
                        .ToListAsync();

                    foreach (var mesa in mesas)
                    {
                        mesa.GarzonId = garzon.Id;
                    }
                    await _context.SaveChangesAsync();
                }
            }
        }
    }

    private static string GenerarCodigoQr()
    {
        // Generar un código QR aleatorio en base64 URL-safe
        var bytes = new byte[8];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return System.Text.Encodings.Web.UrlEncoder.Default.Encode(Convert.ToBase64String(bytes));
    }
}
