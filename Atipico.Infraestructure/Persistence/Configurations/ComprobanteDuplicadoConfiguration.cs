using Atipico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class ComprobanteDuplicadoConfiguration : IEntityTypeConfiguration<ComprobanteDuplicado>
    {
        public void Configure(EntityTypeBuilder<ComprobanteDuplicado> builder)
        {
            builder.ToView("v_comprobante_duplicado");
            builder.HasNoKey();

            builder.Property(c => c.Id).HasColumnName("id");
            builder.Property(c => c.IdCuenta).HasColumnName("id_cuenta");
            builder.Property(c => c.HashSha256).HasColumnName("hash_sha256");
            builder.Property(c => c.Monto).HasColumnName("monto");
            builder.Property(c => c.CreadoEn).HasColumnName("creado_en");
            builder.Property(c => c.IdSubidoPor).HasColumnName("id_subido_por");
        }
    }
}
