using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PedidoMesaConfiguration : IEntityTypeConfiguration<PedidoMesa>
    {
        public void Configure(EntityTypeBuilder<PedidoMesa> builder)
        {
            builder.ToTable("pedido_mesa");

            builder.HasKey(pm => pm.Id);
            builder.Property(pm => pm.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(pm => pm.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            builder.Property(pm => pm.IdPedido).HasColumnName("id_pedido");
            builder.HasOne(pm => pm.Pedido)
                .WithMany(p => p.PedidoMesas)
                .HasForeignKey(pm => pm.IdPedido)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(pm => pm.IdMesa).HasColumnName("id_mesa");
            builder.HasOne(pm => pm.Mesa)
                .WithMany(m => m.PedidoMesas)
                .HasForeignKey(pm => pm.IdMesa)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(pm => new { pm.IdPedido, pm.IdMesa }).IsUnique().HasDatabaseName("uk_pedido_mesa");
            builder.HasIndex(pm => pm.IdMesa).HasDatabaseName("ix_pedido_mesa_mesa");
        }
    }
}
