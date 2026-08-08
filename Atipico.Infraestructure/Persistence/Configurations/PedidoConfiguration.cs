using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
    {
        public void Configure(EntityTypeBuilder<Pedido> builder)
        {
            builder.ToTable("pedido");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();

            builder.Property(p => p.Comensal).HasColumnName("comensal").HasMaxLength(120);

            builder.Property(p => p.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoPedido>()).HasMaxLength(20).IsRequired();

            builder.Property(p => p.CreadoEn).HasColumnName("creado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en")
                .HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
            builder.Property(p => p.CerradoEn).HasColumnName("cerrado_en");

            builder.Property(p => p.IdMesero).HasColumnName("id_mesero");
            builder.HasIndex(p => p.IdMesero).HasDatabaseName("ix_pedido_mesero");
            builder.HasIndex(p => p.CreadoEn).HasDatabaseName("ix_pedido_fecha");
        }
    }
}
