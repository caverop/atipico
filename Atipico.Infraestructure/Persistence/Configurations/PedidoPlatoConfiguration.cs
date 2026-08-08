using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PedidoPlatoConfiguration : IEntityTypeConfiguration<PedidoPlato>
    {
        public void Configure(EntityTypeBuilder<PedidoPlato> builder)
        {
            builder.ToTable("pedido_plato");

            builder.HasKey(pp => pp.Id);
            builder.Property(pp => pp.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(pp => pp.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoPedidoPlato>()).HasMaxLength(20).IsRequired();

            builder.Property(pp => pp.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(pp => pp.ServidoEn).HasColumnName("servido_en");
            builder.Property(pp => pp.AnuladoEn).HasColumnName("anulado_en");
            builder.Property(pp => pp.MotivoAnulacion).HasColumnName("motivo_anulacion");

            builder.Property(pp => pp.IdPedido).HasColumnName("id_pedido");
            builder.HasOne(pp => pp.Pedido)
                .WithMany(p => p.PedidoPlatos)
                .HasForeignKey(pp => pp.IdPedido)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(pp => pp.IdPedido).HasDatabaseName("ix_pedido_plato_pedido");

            builder.Property(pp => pp.IdPlato).HasColumnName("id_plato");
            builder.HasOne(pp => pp.Plato)
                .WithMany(p => p.PedidoPlatos)
                .HasForeignKey(pp => pp.IdPlato)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(pp => pp.IdPlato).HasDatabaseName("ix_pedido_plato_plato");

            builder.Property(pp => pp.IdAnuladoPor).HasColumnName("id_anulado_por");
        }
    }
}
