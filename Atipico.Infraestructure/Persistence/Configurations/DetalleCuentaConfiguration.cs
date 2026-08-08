using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class DetalleCuentaConfiguration : IEntityTypeConfiguration<DetalleCuenta>
    {
        public void Configure(EntityTypeBuilder<DetalleCuenta> builder)
        {
            builder.ToTable("detalle_cuenta", t => t.HasCheckConstraint("ck_detalle_precio", "precio_unitario >= 0"));

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(10, 2);

            builder.Property(d => d.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            builder.Property(d => d.IdCuenta).HasColumnName("id_cuenta");
            builder.HasOne(d => d.Cuenta)
                .WithMany(c => c.DetalleCuentas)
                .HasForeignKey(d => d.IdCuenta)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(d => d.IdCuenta).HasDatabaseName("ix_detalle_cuenta_cuenta");

            builder.Property(d => d.IdPedidoPlato).HasColumnName("id_pedido_plato");
            builder.HasOne(d => d.PedidoPlato)
                .WithOne(pp => pp.DetalleCuenta)
                .HasForeignKey<DetalleCuenta>(d => d.IdPedidoPlato)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(d => d.IdPedidoPlato).IsUnique().HasDatabaseName("uk_detalle_pedido_plato");
        }
    }
}
