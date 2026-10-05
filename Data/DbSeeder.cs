using EatWeb.Models;
using EatWeb.Models.Enums;
using EatWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly RestauranteDefaultsService _defaults;

    public DbSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        RestauranteDefaultsService defaults)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _defaults = defaults;
    }

    public async Task SembrarAsync(IServiceProvider serviceProvider)
    {
        await _context.Database.MigrateAsync();

        // ===== 1. Roles =====
        var roles = new[]
        {
            Roles.AdminCadena,
            Roles.AdminRestaurante,
            Roles.Garzon
        };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }

        // ===== 2. Restaurante Demo =====
        var restaurante = await _context.Restaurantes
            .FirstOrDefaultAsync(r => r.Nombre == "Restaurante Demo");

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

        await _defaults.AsegurarEstacionesAsync(restaurante.Id);

        // ===== 3. Admin Restaurante =====
        var adminEmail = _configuration["SeedAdmin:Email"]
                         ?? "admin@restaurante.local";
        var admin = await _userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            var adminPassword = _configuration["SeedAdmin:Password"]
                                ?? "Admin123!";
            var adminNombre = _configuration["SeedAdmin:Nombre"]
                              ?? "Administrador";

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                NombreCompleto = adminNombre,
                Activo = true
            };

            var result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, Roles.AdminRestaurante);

                _context.RestaurantesMiembros.Add(new RestauranteMiembro
                {
                    RestauranteId = restaurante.Id,
                    UsuarioId = admin.Id,
                    Rol = RolRestaurante.Administrador
                });

                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Asegurar membresía si el admin ya existía
            var tieneMembresia = await _context.RestaurantesMiembros
                .AnyAsync(rm =>
                    rm.UsuarioId == admin.Id &&
                    rm.RestauranteId == restaurante.Id);

            if (!tieneMembresia)
            {
                _context.RestaurantesMiembros.Add(new RestauranteMiembro
                {
                    RestauranteId = restaurante.Id,
                    UsuarioId = admin.Id,
                    Rol = RolRestaurante.Administrador
                });
                await _context.SaveChangesAsync();
            }
        }

        // ===== 4. Garzón demo =====
        var garzonEmail = "garzon@restaurante.local";
        var garzon = await _userManager.FindByEmailAsync(garzonEmail);

        if (garzon == null)
        {
            garzon = new ApplicationUser
            {
                UserName = garzonEmail,
                Email = garzonEmail,
                NombreCompleto = "Carlos Garzón",
                Activo = true
            };

            var result = await _userManager.CreateAsync(garzon, "Garzon123!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(garzon, Roles.Garzon);

                _context.RestaurantesMiembros.Add(new RestauranteMiembro
                {
                    RestauranteId = restaurante.Id,
                    UsuarioId = garzon.Id,
                    Rol = RolRestaurante.Garzon
                });

                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var tieneMembresia = await _context.RestaurantesMiembros
                .AnyAsync(rm =>
                    rm.UsuarioId == garzon.Id &&
                    rm.RestauranteId == restaurante.Id);

            if (!tieneMembresia)
            {
                _context.RestaurantesMiembros.Add(new RestauranteMiembro
                {
                    RestauranteId = restaurante.Id,
                    UsuarioId = garzon.Id,
                    Rol = RolRestaurante.Garzon
                });
                await _context.SaveChangesAsync();
            }
        }

        // ===== 5. Datos demo (categorías, productos, mesas) =====
        if (!await _context.Categorias.AnyAsync())
        {
            await SembrarCatalogoDemoAsync(restaurante.Id);
        }

        // Asignar mesas 1-3 al garzón demo (si aún no las tiene)
        if (garzon != null)
        {
            var mesas = await _context.Mesas
                .Where(m => m.RestauranteId == restaurante.Id &&
                            new[] { 1, 2, 3 }.Contains(m.Numero) &&
                            m.GarzonId == null)
                .ToListAsync();

            if (mesas.Any())
            {
                foreach (var mesa in mesas)
                    mesa.GarzonId = garzon.Id;

                await _context.SaveChangesAsync();
            }
        }

        // ===== 6. Cadena demo + Admin de Cadena =====
        var cadenaNombre = "Burger King Demo";
        var cadena = await _context.Cadenas
            .FirstOrDefaultAsync(c => c.Nombre == cadenaNombre);

        if (cadena == null)
        {
            cadena = new Cadena { Nombre = cadenaNombre };
            _context.Cadenas.Add(cadena);
            await _context.SaveChangesAsync();

            // Vincular el Restaurante Demo a la cadena
            restaurante.CadenaId = cadena.Id;
            await _context.SaveChangesAsync();
        }

        var adminCadenaEmail = "admincadena@demo.local";
        var adminCadena = await _userManager.FindByEmailAsync(adminCadenaEmail);

        if (adminCadena == null)
        {
            adminCadena = new ApplicationUser
            {
                UserName = adminCadenaEmail,
                Email = adminCadenaEmail,
                NombreCompleto = "Admin de Cadena",
                Activo = true
            };

            var result = await _userManager.CreateAsync(adminCadena, "Cadena123!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminCadena, Roles.AdminCadena);

                _context.CadenasMiembros.Add(new CadenaMiembro
                {
                    CadenaId = cadena.Id,
                    UsuarioId = adminCadena.Id,
                    Rol = RolCadena.Administrador
                });

                await _context.SaveChangesAsync();
            }
        }
        else
        {
            if (!await _userManager.IsInRoleAsync(adminCadena, Roles.AdminCadena))
                await _userManager.AddToRoleAsync(adminCadena, Roles.AdminCadena);

            if (!await _context.CadenasMiembros.AnyAsync(cm =>
                    cm.CadenaId == cadena.Id && cm.UsuarioId == adminCadena.Id))
            {
                _context.CadenasMiembros.Add(new CadenaMiembro
                {
                    CadenaId = cadena.Id,
                    UsuarioId = adminCadena.Id,
                    Rol = RolCadena.Administrador
                });
                await _context.SaveChangesAsync();
            }
        }
    }


    // ============ Catálogo + mesas ============

    private async Task SembrarCatalogoDemoAsync(int restauranteId)
    {
        await _defaults.AsegurarEstacionesAsync(restauranteId);

        var estaciones = await _context.Estaciones
            .AsNoTracking()
            .Where(e => e.RestauranteId == restauranteId && (e.Nombre == "Cocina" || e.Nombre == "Bar"))
            .ToDictionaryAsync(e => e.Nombre, e => e.Id);

        var cocinaId = estaciones["Cocina"];
        var barId = estaciones["Bar"];

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
                RestauranteId = restauranteId,
                EstacionId = kvp.Key == "Bebidas" ? barId : cocinaId,
                Nombre = kvp.Key,
                Orden = kvp.Value,
                Activa = true
            };
            _context.Categorias.Add(categoria);
            categoriasDb[kvp.Key] = categoria;
        }
        await _context.SaveChangesAsync();

        var ingredientesNombres = new[]
        {
            "Salsa de tomate", "Queso", "Pepperoni", "Aceitunas", "Champiñones",
            "Tocino", "Tomate", "Albahaca", "Pan", "Carne",
            "Lechuga", "Palta", "Huevo"
        };

        var ingredientesDb = new Dictionary<string, Ingrediente>();
        foreach (var nombre in ingredientesNombres)
        {
            var ing = new Ingrediente
            {
                RestauranteId = restauranteId,
                Nombre = nombre,
                Activo = true
            };
            _context.Ingredientes.Add(ing);
            ingredientesDb[nombre] = ing;
        }
        await _context.SaveChangesAsync();

        // Productos
        var pizzaAmericana = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Pizzas"].Id,
            Nombre = "Pizza Americana",
            Descripcion = "Pizza deliciosa con ingredientes especiales",
            Precio = 12990,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var pizzaNapolitana = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Pizzas"].Id,
            Nombre = "Pizza Napolitana",
            Descripcion = "Clásica pizza italiana",
            Precio = 11990,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var hamburguesa = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Hamburguesas"].Id,
            Nombre = "Hamburguesa Clásica",
            Descripcion = "Deliciosa hamburguesa artesanal",
            Precio = 8990,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var papas = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Acompañamientos"].Id,
            Nombre = "Papas Fritas",
            Descripcion = "Papas fritas crujientes",
            Precio = 3990,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var coca = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Bebidas"].Id,
            Nombre = "Coca-Cola",
            Descripcion = "Bebida refrescante",
            Precio = 3000,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var cerveza = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Bebidas"].Id,
            Nombre = "Cerveza",
            Descripcion = "Cerveza fría",
            Precio = 3500,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };
        var brownie = new Producto
        {
            RestauranteId = restauranteId,
            CategoriaId = categoriasDb["Postres"].Id,
            Nombre = "Brownie",
            Descripcion = "Brownie de chocolate",
            Precio = 3900,
            Activo = true,
            Disponible = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Productos.AddRange(
            pizzaAmericana, pizzaNapolitana, hamburguesa,
            papas, coca, cerveza, brownie);
        await _context.SaveChangesAsync();

        // Personalizaciones
        void AgregarIngredientes(Producto p, (string nombre, string tipo, decimal precio)[] items)
        {
            foreach (var (nombre, tipo, precio) in items)
            {
                _context.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    ProductoId = p.Id,
                    IngredienteId = ingredientesDb[nombre].Id,
                    Tipo = tipo,
                    PrecioExtra = precio
                });
            }
        }

        AgregarIngredientes(pizzaAmericana, new[]
        {
            ("Salsa de tomate", TipoIngrediente.Incluido, 0m),
            ("Queso",           TipoIngrediente.Incluido, 0m),
            ("Pepperoni",       TipoIngrediente.Incluido, 0m),
            ("Aceitunas",       TipoIngrediente.Incluido, 0m),
            ("Champiñones",     TipoIngrediente.Extra,    0m),
            ("Tocino",          TipoIngrediente.Extra,    1200m)
        });

        AgregarIngredientes(pizzaNapolitana, new[]
        {
            ("Salsa de tomate", TipoIngrediente.Incluido, 0m),
            ("Queso",           TipoIngrediente.Incluido, 0m),
            ("Tomate",          TipoIngrediente.Incluido, 0m),
            ("Albahaca",        TipoIngrediente.Incluido, 0m),
            ("Aceitunas",       TipoIngrediente.Extra,    800m),
            ("Champiñones",     TipoIngrediente.Extra,    900m)
        });

        AgregarIngredientes(hamburguesa, new[]
        {
            ("Pan",     TipoIngrediente.Incluido, 0m),
            ("Carne",   TipoIngrediente.Incluido, 0m),
            ("Lechuga", TipoIngrediente.Incluido, 0m),
            ("Tomate",  TipoIngrediente.Incluido, 0m),
            ("Queso",   TipoIngrediente.Incluido, 0m),
            ("Tocino",  TipoIngrediente.Extra,    1200m),
            ("Palta",   TipoIngrediente.Extra,    1000m),
            ("Huevo",   TipoIngrediente.Extra,    800m)
        });

        await _context.SaveChangesAsync();

        // Mesas
        for (int i = 1; i <= 5; i++)
        {
            _context.Mesas.Add(new Mesa
            {
                RestauranteId = restauranteId,
                Numero = i,
                CodigoQr = QrHelper.GenerarCodigo(),
                Activa = true
            });
        }
        await _context.SaveChangesAsync();
    }
}