using Microsoft.EntityFrameworkCore;
using Outshot.Models;

namespace Outshot.Data
{
    public class OutshotContext : DbContext
    {
        public OutshotContext(DbContextOptions<OutshotContext> options)
            : base(options)
        {
        }

        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Inventario> Inventarios { get; set; }
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallePedido> DetallesPedidos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MovimientoInventario>()
                .HasKey(m => m.MovimientoId);

            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<DetallePedido>()
                .Property(d => d.PrecioUnitario)
                .HasPrecision(18, 2);

            modelBuilder.Entity<DetallePedido>()
                .Property(d => d.Subtotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Categoria>()
                .HasMany(c => c.Productos)
                .WithOne(p => p.Categoria)
                .HasForeignKey(p => p.CategoriaId);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Inventario)
                .WithOne(i => i.Producto)
                .HasForeignKey<Inventario>(i => i.ProductoId);

            modelBuilder.Entity<Inventario>()
                .HasMany(i => i.MovimientosInventario)
                .WithOne(m => m.Inventario)
                .HasForeignKey(m => m.InventarioId);

            modelBuilder.Entity<Usuario>()
                .HasMany(u => u.MovimientosInventario)
                .WithOne(m => m.Usuario)
                .HasForeignKey(m => m.UsuarioId);

            modelBuilder.Entity<Cliente>()
                .HasMany(c => c.Pedidos)
                .WithOne(p => p.Cliente)
                .HasForeignKey(p => p.ClienteId);

            modelBuilder.Entity<Usuario>()
                .HasMany(u => u.Pedidos)
                .WithOne(p => p.Usuario)
                .HasForeignKey(p => p.UsuarioId);

            modelBuilder.Entity<Pedido>()
                .HasMany(p => p.DetallePedidos)
                .WithOne(d => d.Pedido)
                .HasForeignKey(d => d.PedidoId);

            modelBuilder.Entity<Producto>()
                .HasMany(p => p.DetallePedidos)
                .WithOne(d => d.Producto)
                .HasForeignKey(d => d.ProductoId);
        }
    }
}