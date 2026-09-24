using EatWeb.Models;
using EatWeb.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EatWeb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Restaurante> Restaurantes { get; set; } = null!;
    public DbSet<Mesa> Mesas { get; set; } = null!;
    public DbSet<MesaSesion> MesaSesiones { get; set; } = null!;
    public DbSet<Comensal> Comensales { get; set; } = null!;
    public DbSet<Categoria> Categorias { get; set; } = null!;
    public DbSet<Producto> Productos { get; set; } = null!;
    public DbSet<Ingrediente> Ingredientes { get; set; } = null!;
    public DbSet<ProductoIngrediente> ProductoIngredientes { get; set; } = null!;
    public DbSet<Pedido> Pedidos { get; set; } = null!;
    public DbSet<DetallePedido> DetallesPedidos { get; set; } = null!;
    public DbSet<DetallePedidoIngrediente> DetallesIngredientes { get; set; } = null!;
    public DbSet<HistorialEstadoPedido> HistorialesEstadoPedido { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser - agregar propiedades requeridas
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.NombreCompleto)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.RestauranteId)
            .IsRequired();

        // Restaurante
        modelBuilder.Entity<Restaurante>()
            .Property(r => r.Nombre)
            .HasMaxLength(120)
            .IsRequired();

        modelBuilder.Entity<Restaurante>()
            .Property(r => r.LogoUrl)
            .HasMaxLength(300);

        // Mesa
        modelBuilder.Entity<Mesa>()
            .Property(m => m.CodigoQr)
            .HasMaxLength(32)
            .IsRequired();

        modelBuilder.Entity<Mesa>()
            .HasIndex(m => new { m.RestauranteId, m.Numero })
            .IsUnique();

        modelBuilder.Entity<Mesa>()
            .HasIndex(m => m.CodigoQr)
            .IsUnique();

        modelBuilder.Entity<Mesa>()
            .HasOne(m => m.Restaurante)
            .WithMany(r => r.Mesas)
            .HasForeignKey(m => m.RestauranteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Mesa>()
            .HasOne(m => m.Garzon)
            .WithMany(u => u.Mesas)
            .HasForeignKey(m => m.GarzonId)
            .OnDelete(DeleteBehavior.SetNull);

        // MesaSesion
        modelBuilder.Entity<MesaSesion>()
            .HasIndex(s => s.MesaId)
            .IsUnique()
            .HasFilter("[FechaCierre] IS NULL");

        modelBuilder.Entity<MesaSesion>()
            .HasOne(s => s.Mesa)
            .WithMany(m => m.Sesiones)
            .HasForeignKey(s => s.MesaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comensal
        modelBuilder.Entity<Comensal>()
            .Property(c => c.Nombre)
            .HasMaxLength(40)
            .IsRequired();

        modelBuilder.Entity<Comensal>()
            .HasIndex(c => c.Token)
            .IsUnique();

        modelBuilder.Entity<Comensal>()
            .HasIndex(c => c.MesaSesionId);

        modelBuilder.Entity<Comensal>()
            .HasOne(c => c.MesaSesion)
            .WithMany(s => s.Comensales)
            .HasForeignKey(c => c.MesaSesionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Categoria
        modelBuilder.Entity<Categoria>()
            .Property(c => c.Nombre)
            .HasMaxLength(80)
            .IsRequired();

        modelBuilder.Entity<Categoria>()
            .HasIndex(c => new { c.RestauranteId, c.Nombre })
            .IsUnique();

        modelBuilder.Entity<Categoria>()
            .HasOne(c => c.Restaurante)
            .WithMany(r => r.Categorias)
            .HasForeignKey(c => c.RestauranteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Producto
        modelBuilder.Entity<Producto>()
            .Property(p => p.Nombre)
            .HasMaxLength(120)
            .IsRequired();

        modelBuilder.Entity<Producto>()
            .Property(p => p.Descripcion)
            .HasMaxLength(500);

        modelBuilder.Entity<Producto>()
            .Property(p => p.ImagenUrl)
            .HasMaxLength(300);

        modelBuilder.Entity<Producto>()
            .Property(p => p.Precio)
            .HasPrecision(18, 0);

        modelBuilder.Entity<Producto>()
            .HasIndex(p => new { p.RestauranteId, p.CategoriaId });

        modelBuilder.Entity<Producto>()
            .HasOne(p => p.Restaurante)
            .WithMany(r => r.Productos)
            .HasForeignKey(p => p.RestauranteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Producto>()
            .HasOne(p => p.Categoria)
            .WithMany(c => c.Productos)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ingrediente
        modelBuilder.Entity<Ingrediente>()
            .Property(i => i.Nombre)
            .HasMaxLength(80)
            .IsRequired();

        modelBuilder.Entity<Ingrediente>()
            .HasIndex(i => new { i.RestauranteId, i.Nombre })
            .IsUnique();

        modelBuilder.Entity<Ingrediente>()
            .HasOne(i => i.Restaurante)
            .WithMany(r => r.Ingredientes)
            .HasForeignKey(i => i.RestauranteId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProductoIngrediente
        modelBuilder.Entity<ProductoIngrediente>()
            .HasKey(pi => new { pi.ProductoId, pi.IngredienteId });

        modelBuilder.Entity<ProductoIngrediente>()
            .Property(pi => pi.PrecioExtra)
            .HasPrecision(18, 0);

        modelBuilder.Entity<ProductoIngrediente>()
            .Property(pi => pi.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<ProductoIngrediente>()
            .HasOne(pi => pi.Producto)
            .WithMany(p => p.Ingredientes)
            .HasForeignKey(pi => pi.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductoIngrediente>()
            .HasOne(pi => pi.Ingrediente)
            .WithMany(i => i.Productos)
            .HasForeignKey(pi => pi.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Pedido
        modelBuilder.Entity<Pedido>()
            .Property(p => p.Estado)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<Pedido>()
            .Property(p => p.Total)
            .HasPrecision(18, 0);

        modelBuilder.Entity<Pedido>()
            .Property(p => p.ObservacionGeneral)
            .HasMaxLength(300);

        modelBuilder.Entity<Pedido>()
            .HasIndex(p => p.MesaSesionId);

        modelBuilder.Entity<Pedido>()
            .HasIndex(p => new { p.Estado, p.FechaCreacion });

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.MesaSesion)
            .WithMany(s => s.Pedidos)
            .HasForeignKey(p => p.MesaSesionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Comensal)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.ComensalId)
            .OnDelete(DeleteBehavior.Restrict);

        // DetallePedido
        modelBuilder.Entity<DetallePedido>()
            .Property(d => d.NombreProducto)
            .HasMaxLength(120)
            .IsRequired();

        modelBuilder.Entity<DetallePedido>()
            .Property(d => d.PrecioUnitario)
            .HasPrecision(18, 0);

        modelBuilder.Entity<DetallePedido>()
            .Property(d => d.Observacion)
            .HasMaxLength(300);

        modelBuilder.Entity<DetallePedido>()
            .Property(d => d.Subtotal)
            .HasPrecision(18, 0);

        modelBuilder.Entity<DetallePedido>()
            .HasOne(d => d.Pedido)
            .WithMany(p => p.Detalles)
            .HasForeignKey(d => d.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DetallePedido>()
            .HasOne(d => d.Producto)
            .WithMany(p => p.Detalles)
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        // DetallePedidoIngrediente
        modelBuilder.Entity<DetallePedidoIngrediente>()
            .Property(d => d.NombreIngrediente)
            .HasMaxLength(80)
            .IsRequired();

        modelBuilder.Entity<DetallePedidoIngrediente>()
            .Property(d => d.Accion)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<DetallePedidoIngrediente>()
            .Property(d => d.PrecioExtra)
            .HasPrecision(18, 0);

        modelBuilder.Entity<DetallePedidoIngrediente>()
            .HasOne(d => d.DetallePedido)
            .WithMany(d => d.Ingredientes)
            .HasForeignKey(d => d.DetallePedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DetallePedidoIngrediente>()
            .HasOne(d => d.Ingrediente)
            .WithMany(i => i.DetallesIngredientes)
            .HasForeignKey(d => d.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // HistorialEstadoPedido
        modelBuilder.Entity<HistorialEstadoPedido>()
            .Property(h => h.EstadoAnterior)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<HistorialEstadoPedido>()
            .Property(h => h.EstadoNuevo)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<HistorialEstadoPedido>()
            .HasOne(h => h.Pedido)
            .WithMany(p => p.Historial)
            .HasForeignKey(h => h.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HistorialEstadoPedido>()
            .HasOne(h => h.Usuario)
            .WithMany(u => u.HistorialesEstadoPedido)
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
