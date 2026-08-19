using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class CuentaQrEvidenciaIncompletaConfiguration : IEntityTypeConfiguration<CuentaQrEvidenciaIncompleta>
    {
        public void Configure(EntityTypeBuilder<CuentaQrEvidenciaIncompleta> builder)
        {
            builder.ToView("v_cuenta_qr_evidencia_incompleta");
            builder.HasNoKey();

            builder.Property(c => c.Id).HasColumnName("id");
            builder.Property(c => c.Comensal).HasColumnName("comensal");
            builder.Property(c => c.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoCuenta>());
            builder.Property(c => c.Monto).HasColumnName("monto");
            builder.Property(c => c.PagadoEn).HasColumnName("pagado_en");
            builder.Property(c => c.IdMesero).HasColumnName("id_mesero");
            builder.Property(c => c.Comprobantes).HasColumnName("comprobantes");
            builder.Property(c => c.SinMonto).HasColumnName("sin_monto");
            builder.Property(c => c.MontoRespaldado).HasColumnName("monto_respaldado");
        }
    }
}
