using Atipico.Domain.Entities;
using Atipico.Domain.Enums;
using Atipico.Infraestructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atipico.Infraestructure.Persistence.Configurations
{
    public class CuentaDescuadradaConfiguration : IEntityTypeConfiguration<CuentaDescuadrada>
    {
        public void Configure(EntityTypeBuilder<CuentaDescuadrada> builder)
        {
            builder.ToView("v_cuenta_descuadrada");
            builder.HasNoKey();

            builder.Property(c => c.Id).HasColumnName("id");
            builder.Property(c => c.Estado).HasColumnName("estado")
                .HasConversion(new UpperSnakeCaseEnumConverter<EstadoCuenta>());
            builder.Property(c => c.Monto).HasColumnName("monto");
            builder.Property(c => c.SumaDetalle).HasColumnName("suma_detalle");
            builder.Property(c => c.PagadoEn).HasColumnName("pagado_en");
        }
    }
}
