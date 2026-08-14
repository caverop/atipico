using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Infraestructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            optionsBuilder
                .UseNpgsql(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<TipoPlato> TipoPlatos => Set<TipoPlato>();
        public DbSet<Plato> Platos => Set<Plato>();
        public DbSet<Mesa> Mesas => Set<Mesa>();
        public DbSet<Pedido> Pedidos => Set<Pedido>();
        public DbSet<PedidoMesa> PedidoMesas => Set<PedidoMesa>();
        public DbSet<PedidoPlato> PedidoPlatos => Set<PedidoPlato>();
        public DbSet<Cuenta> Cuentas => Set<Cuenta>();
        public DbSet<DetalleCuenta> DetalleCuentas => Set<DetalleCuenta>();

        // Vistas de solo lectura (ver sql/script_inicial.sql): sin PK real, nunca se
        // crean/actualizan/borran filas de estos DbSet, solo se consultan.
        public DbSet<PedidoPlatoSinCobrar> PedidoPlatosSinCobrar => Set<PedidoPlatoSinCobrar>();
        public DbSet<CuentaDescuadrada> CuentasDescuadradas => Set<CuentaDescuadrada>();
    }
}
