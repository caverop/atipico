using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class PedidoPlatoSinCobrarConfiguration : IEntityTypeConfiguration<PedidoPlatoSinCobrar>
    {
        public void Configure(EntityTypeBuilder<PedidoPlatoSinCobrar> builder)
        {
            builder.ToView("v_pedido_plato_sin_cobrar");
            builder.HasNoKey();

            builder.Property(p => p.Id).HasColumnName("id_pedido_plato");
            builder.Property(p => p.IdPedido).HasColumnName("id_pedido");
            builder.Property(p => p.Plato).HasColumnName("plato");
            builder.Property(p => p.Precio).HasColumnName("precio");
            builder.Property(p => p.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoPedidoPlato>());
        }
    }
}
