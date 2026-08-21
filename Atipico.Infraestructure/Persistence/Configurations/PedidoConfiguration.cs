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

            // uk_pedido_comensal_activo (sql/003_pedido_comensal_unico.sql): dos pedidos
            // Abierto/EnPreparacion no pueden compartir comensal.
            builder.HasIndex(p => p.Comensal)
                .IsUnique()
                .HasDatabaseName("uk_pedido_comensal_activo")
                .HasFilter("estado IN ('ABIERTO', 'EN_PREPARACION')");

            builder.Property(p => p.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoPedido>()).HasMaxLength(20).IsRequired();

            // ck_pedido_tipo (sql/008_pedido_tipo.sql). El DEFAULT 'EN_SALON' vive en la
            // base para las filas que ya existian; aca no se declara HasDefaultValue porque
            // la entidad siempre manda un valor explicito.
            builder.Property(p => p.Tipo).HasColumnName("tipo")
                .HasConversion(new UpperSnakeCaseEnumConverter<TipoPedido>()).HasMaxLength(20).IsRequired();

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
